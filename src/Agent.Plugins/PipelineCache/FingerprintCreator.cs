﻿using Agent.Sdk;
using BuildXL.Cache.ContentStore.Interfaces.Utils;
using Microsoft.VisualStudio.Services.PipelineCache.WebApi;
using Minimatch;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

[assembly: InternalsVisibleTo("Test")]

namespace Agent.Plugins.PipelineCache
{
    public static class FingerprintCreator
    {
        private static readonly bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private static readonly bool isCaseSensitive = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        // https://github.com/Microsoft/azure-pipelines-task-lib/blob/master/node/docs/findingfiles.md#matchoptions
        private static readonly Options minimatchOptions = new Options
        {
            Dot = true,
            NoBrace = true,
            NoCase = !isCaseSensitive,
            AllowWindowsPaths = isWindows,
        };

        private static readonly char[] GlobChars = new [] { '*', '?', '[', ']' };

        private const char ForceStringLiteral = '"';

        private static bool IsPathyChar(char c)
        {
            if (GlobChars.Contains(c)) return true;
            if (c == Path.DirectorySeparatorChar) return true;
            if (c == Path.AltDirectorySeparatorChar) return true;
            if (c == Path.VolumeSeparatorChar) return true;
            return !Path.GetInvalidFileNameChars().Contains(c);
        }

        internal static bool IsPathyKeySegment(string keySegment)
        {
            if (keySegment.First() == ForceStringLiteral && keySegment.Last() == ForceStringLiteral) return false;
            if (keySegment.Any(c => !IsPathyChar(c))) return false;
            if (!keySegment.Contains(".") && 
                !keySegment.Contains(Path.DirectorySeparatorChar) &&
                !keySegment.Contains(Path.AltDirectorySeparatorChar)) return false;
            if (keySegment.Last() == '.') return false;
            return true;
        }

        internal static Func<string, bool> CreateMinimatchFilter(AgentTaskPluginExecutionContext context, string rule, bool invert)
        {
            Func<string,bool> filter = Minimatcher.CreateFilter(rule, minimatchOptions);
            Func<string,bool> tracedFilter = (path) => {
                bool filterResult = filter(path);
                context.Verbose($"Path `{path}` is {(filterResult ? "" : "not ")}{(invert ? "excluded" : "included")} because of pattern `{(invert ? "!" : "")}{rule}`.");
                return invert ^ filterResult;
            };

            return tracedFilter;
        }

        internal static string MakePathCanonical(string defaultWorkingDirectory, string path)
        {
            // Normalize to some extent, let minimatch worry about casing
            if (Path.IsPathFullyQualified(path))
            {
                return Path.GetFullPath(path);
            }
            else
            {
                return Path.GetFullPath(path, defaultWorkingDirectory);
            }
        }

        internal static Func<string,bool> CreateFilter(
            AgentTaskPluginExecutionContext context,
            IEnumerable<string> includeRules,
            IEnumerable<string> excludeRules)
        {
            Func<string,bool>[] includeFilters = includeRules.Select(includeRule =>
                CreateMinimatchFilter(context, includeRule, invert: false)).ToArray();
            Func<string,bool>[] excludeFilters = excludeRules.Select(excludeRule => 
                CreateMinimatchFilter(context, excludeRule, invert: true)).ToArray();
            Func<string,bool> filter = (path) => includeFilters.Any(f => f(path)) && excludeFilters.All(f => f(path));
            return filter;
        }

        internal struct Enumeration
        {
            public string RootPath;
            public string Pattern;
            public SearchOption Depth;
        }

        internal struct MatchedFile
        {
            public string DisplayPath;
            public long FileLength;
            public string Hash;
        }

        internal enum KeySegmentType
        {
            String = 0,
            FilePath = 1,
            FilePattern = 2
        }

        // Given a globby path, figure out where to start enumerating.
        // Room for optimization here e.g. 
        // includeGlobPath = /dir/*foo* 
        // should map to 
        // enumerateRootPath = /dir/
        // enumeratePattern = *foo*
        // enumerateDepth = SearchOption.TopDirectoryOnly
        //
        // It's ok to come up with a file-enumeration that includes too much as the glob filter
        // will filter out the extra, but it's not ok to include too little in the enumeration.
        internal static Enumeration DetermineFileEnumerationFromGlob(string includeGlobPathAbsolute)
        {
            int firstGlob = includeGlobPathAbsolute.IndexOfAny(GlobChars);
            bool hasRecursive = includeGlobPathAbsolute.Contains("**", StringComparison.Ordinal);

            // no globbing
            if (firstGlob < 0)
            {
                return new Enumeration() {
                    RootPath = Path.GetDirectoryName(includeGlobPathAbsolute),
                    Pattern = Path.GetFileName(includeGlobPathAbsolute),
                    Depth = SearchOption.TopDirectoryOnly
                };
            }
            else
            {
                int rootDirLength = includeGlobPathAbsolute.Substring(0,firstGlob).LastIndexOfAny( new [] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar});
                return new Enumeration() {
                    RootPath = includeGlobPathAbsolute.Substring(0,rootDirLength),
                    Pattern = "*",
                    Depth = hasRecursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly
                };
            }
        }

        internal static void CheckKeySegment(string keySegment)
        {
            if (keySegment.Equals("*", StringComparison.Ordinal))
            {
                throw new ArgumentException("`*` is a reserved key segment. For path glob, use `./*`.");
            }
            else if (keySegment.Equals(Fingerprint.Wildcard, StringComparison.Ordinal))
            {
                throw new ArgumentException("`**` is a reserved key segment. For path glob, use `./**`.");
            }
            else if (keySegment.First() == '\'')
            {
                throw new ArgumentException("A key segment cannot start with a single-quote character`.");
            }
            else if (keySegment.First() == '`')
            {
                throw new ArgumentException("A key segment cannot start with a backtick character`.");
            }
        }

/*
 - jest-v11        [string]
 - Linux           [string]
 - **_package.json [file pattern; 4 matches]
   - packages/babel-jest/package.json              --> CA3D163BAB055381827226140568F3BEF7EAAC187CEBD76878E0B63E9E442356
   - packages/babel-plugin-jest-hoist/package.json --> 75D421513AC39C243147FBF6E8019B8D05A815534E950E165FCA4EDFE2200250
   - packages/babel-preset-jest/package.json       --> DB2AAE67D6E4881DD0104E61478B00440A6BBD3F41DC88F780FDEEEE17690D3A
   - packages/diff-sequences/package.json          --> 05D421513AC39C243147FBF6E8019B8D05A815534E950E165FCA4EDFE2200250
 - 15685           [string]

 */

        //internal delegate void KeySegmentLogger(string segment, string segmentType, string? details);

        public static Fingerprint CreateFromKey(
            AgentTaskPluginExecutionContext context,
            IEnumerable<string> keySegments,
            string filePathRoot)
        {
            var sha256 = new SHA256Managed();

            string defaultWorkingDirectory = context.Variables.GetValueOrDefault(
                "system.defaultworkingdirectory" // Constants.Variables.System.DefaultWorkingDirectory
                )?.Value;

            var resolvedSegments = new List<string>();

            foreach (string keySegment in keySegments)
            {
                CheckKeySegment(keySegment);
            }

            Func<string,int,string> FormatStringForDisplay = (value, displayLength) => {
                if (value.Length > displayLength) {
                    value = value.Substring(0, displayLength - 3) + "...";
                }
                return value.PadRight(displayLength);
            };

            Action<string, KeySegmentType, Object> LogKeySegment = (segment, type, details) => {
                string formattedSegment = FormatStringForDisplay(segment, Math.Min(keySegments.Select(s => s.Length).Max(), 50));

                if (type == KeySegmentType.String)
                {
                    context.Output($" - {formattedSegment} [string]");
                }
                else {
                    MatchedFile[] matchedFiles = details as MatchedFile[];
                    
                    if (type == KeySegmentType.FilePath)
                    {
                        string hash = (matchedFiles != null && matchedFiles.Length == 1 ? matchedFiles[0].Hash : null);
                        context.Output($" - {formattedSegment} [file] --> {hash ?? "(not found)"}");
                    }
                    else if (type == KeySegmentType.FilePattern)
                    {
                        context.Output($" - {formattedSegment} [file pattern; matches: {matchedFiles.Length}]");
                        
                        int filePathDisplayLength = Math.Min(keySegments.Select(s => s.Length).Max(), 70);
                        foreach (var matchedFile in matchedFiles) {
                            context.Output($"   - {FormatStringForDisplay(matchedFile.DisplayPath, filePathDisplayLength)} --> {matchedFile.Hash}");
                        }
                    }
                }
            };    

            foreach (string keySegment in keySegments)
            {
                if (!IsPathyKeySegment(keySegment))
                {
                    LogKeySegment(keySegment, KeySegmentType.String, null);
                    resolvedSegments.Add(keySegment);
                }
                else
                {
                    string[] pathRules = keySegment.Split(new []{','}, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();
                    string[] includeRules = pathRules.Where(p => !p.StartsWith('!')).ToArray();

                    if (!includeRules.Any())
                    {
                        throw new ArgumentException("No include rules specified.");
                    }

                    var enumerations = new Dictionary<Enumeration,List<string>>();
                    foreach(string includeRule in includeRules)
                    {
                        string absoluteRootRule = MakePathCanonical(defaultWorkingDirectory, includeRule);
                        context.Verbose($"Expanded include rule is `{absoluteRootRule}`.");
                        Enumeration enumeration = DetermineFileEnumerationFromGlob(absoluteRootRule);
                        List<string> globs;
                        if(!enumerations.TryGetValue(enumeration, out globs))
                        {
                            enumerations[enumeration] = globs = new List<string>(); 
                        }
                        globs.Add(absoluteRootRule);
                    }

                    string[] excludeRules = pathRules.Where(p => p.StartsWith('!')).ToArray();
                    string[] absoluteExcludeRules = excludeRules.Select(excludeRule => {
                        excludeRule = excludeRule.Substring(1);
                        return MakePathCanonical(defaultWorkingDirectory, excludeRule);
                    }).ToArray();

                    var matchedFiles = new SortedDictionary<string, MatchedFile>(StringComparer.Ordinal);

                    foreach(var kvp in enumerations)
                    {
                        Enumeration enumerate = kvp.Key;
                        List<string> absoluteIncludeGlobs = kvp.Value;
                        context.Verbose($"Enumerating starting at root `{enumerate.RootPath}` with pattern `{enumerate.Pattern}` and depth `{enumerate.Depth}`.");
                        IEnumerable<string> files = Directory.EnumerateFiles(enumerate.RootPath, enumerate.Pattern, enumerate.Depth);
                        Func<string,bool> filter = CreateFilter(context, absoluteIncludeGlobs, absoluteExcludeRules);
                        files = files.Where(f => filter(f)).Distinct();

                        foreach(string path in files)
                        {
                            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                byte[] hash = sha256.ComputeHash(fs);
                                // Path.GetRelativePath returns 'The relative path, or path if the paths don't share the same root.'
                                string displayPath = filePathRoot == null ? path : Path.GetRelativePath(filePathRoot, path);
                                matchedFiles.Add(path, new MatchedFile() { DisplayPath = displayPath, FileLength = fs.Length, Hash = hash.ToHex() });
                            }
                        }
                    }

                    string matchedFilesString = matchedFiles.Aggregate(new StringBuilder(),
                        (sb, kvp) => sb.Append($"\nSHA256({kvp.Value.DisplayPath})=[{kvp.Value.FileLength}]{kvp.Value.Hash}"),
                        sb => sb.ToString());

                    context.Debug($"Matched files combined: {matchedFilesString}");

                    LogKeySegment(keySegment, 
                        keySegment.IndexOfAny(GlobChars) >= 0 || matchedFiles.Count() > 1 ? KeySegmentType.FilePattern : KeySegmentType.FilePath,
                        matchedFiles.Values.ToArray());

                    if (!matchedFiles.Any())
                    {
                        throw new FileNotFoundException($"No matching files found for key segment: {keySegment}");
                    }

                    string matchedFilesHash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(matchedFilesString)));
                    resolvedSegments.Add(matchedFilesHash);
                }                 
            }

            return new Fingerprint() { Segments = resolvedSegments.ToArray() };
        }
    }
}