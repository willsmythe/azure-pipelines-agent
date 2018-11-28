using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.Services.Agent.Util;
using Pipelines = Microsoft.TeamFoundation.DistributedTask.Pipelines;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Container
{
    public class ContainerInfo
    {
        private List<MountVolume> _mountVolumes;
        private List<PortMapping> _portMappings;
        private IDictionary<string, string> _environmentVariables;

#if OS_WINDOWS
        private Dictionary<string, string> _pathMappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
#else
        private Dictionary<string, string> _pathMappings = new Dictionary<string, string>();
#endif

        public ContainerInfo(IHostContext hostContext, Pipelines.ContainerResource container, Boolean isJobContainer = true)
        {
            this.ContainerName = container.Alias;

            string containerImage = container.Properties.Get<string>("image");
            ArgUtil.NotNullOrEmpty(containerImage, nameof(containerImage));

            this.ContainerImage = containerImage;
            this.ContainerDisplayName = $"{container.Alias}_{Pipelines.Validation.NameValidation.Sanitize(containerImage)}_{Guid.NewGuid().ToString("N").Substring(0, 6)}";
            this.ContainerRegistryEndpoint = container.Endpoint?.Id ?? Guid.Empty;
            this.ContainerCreateOptions = container.Properties.Get<string>("options");
            this.SkipContainerImagePull = container.Properties.Get<bool>("localimage");
            this.ContainerEnvironmentVariables = container.Environment;
            this.ContainerCommand = container.Properties.Get<string>("command", defaultValue: "");

#if OS_WINDOWS
            _pathMappings[hostContext.GetDirectory(WellKnownDirectory.Tools)] = "C:\\__t"; // Tool cache folder may come from ENV, so we need a unique folder to avoid collision
            _pathMappings[hostContext.GetDirectory(WellKnownDirectory.Work)] = "C:\\__w";
            _pathMappings[hostContext.GetDirectory(WellKnownDirectory.Root)] = "C:\\__a";
#else
            _pathMappings[hostContext.GetDirectory(WellKnownDirectory.Tools)] = "/__t"; // Tool cache folder may come from ENV, so we need a unique folder to avoid collision
            _pathMappings[hostContext.GetDirectory(WellKnownDirectory.Work)] = "/__w";
            _pathMappings[hostContext.GetDirectory(WellKnownDirectory.Root)] = "/__a";
#endif
            this.PortMappings.AddRange(ParsePorts(container.Ports));
            this.MountVolumes.AddRange(ParseVolumes(container.Volumes));
            this.IsJobContainer = isJobContainer;
            if (this.IsJobContainer)
            {
                this.MountVolumes.Add(new MountVolume("/var/run/docker.sock", "/var/run/docker.sock"));
            }
        }

        public string ContainerId { get; set; }
        public string ContainerDisplayName { get; private set; }
        public string ContainerNetwork { get; set; }
        public string ContainerNetworkAlias { get; set; }
        public string ContainerImage { get; set; }
        public string ContainerName { get; set; }
        public string ContainerCommand { get; set; }
        public string ContainerBringNodePath { get; set; }
        public Guid ContainerRegistryEndpoint { get; private set; }
        public string ContainerCreateOptions { get; private set; }
        public bool SkipContainerImagePull { get; private set; }
#if !OS_WINDOWS
        public string CurrentUserName { get; set; }
        public string CurrentUserId { get; set; }
#endif
        public bool IsJobContainer { get; set; }

        public IDictionary<string, string> ContainerEnvironmentVariables
        {
            get
            {
                if (_environmentVariables == null)
                {
                    _environmentVariables = new Dictionary<string, string>();
                }

                return _environmentVariables;
            }
            private set
            {
                _environmentVariables = value;
            }
        }

        public List<MountVolume> MountVolumes
        {
            get
            {
                if (_mountVolumes == null)
                {
                    _mountVolumes = new List<MountVolume>();
                }

                return _mountVolumes;
            }
        }

        public List<PortMapping> PortMappings
        {
            get
            {
                if (_portMappings == null)
                {
                    _portMappings = new List<PortMapping>();
                }

                return _portMappings;
            }
            set
            {
                _portMappings = value;
            }
        }

        public string TranslateToContainerPath(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                foreach (var mapping in _pathMappings)
                {
#if OS_WINDOWS
                    if (string.Equals(path, mapping.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        return mapping.Value;
                    }

                    if (path.StartsWith(mapping.Key + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWith(mapping.Key + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        return mapping.Value + path.Remove(0, mapping.Key.Length);
                    }
#else
                    if (string.Equals(path, mapping.Key))
                    {
                        return mapping.Value;
                    }

                    if (path.StartsWith(mapping.Key + Path.DirectorySeparatorChar))
                    {
                        return mapping.Value + path.Remove(0, mapping.Key.Length);
                    }
#endif
                }
            }

            return path;
        }

        public string TranslateToHostPath(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                foreach (var mapping in _pathMappings)
                {
#if OS_WINDOWS
                    if (string.Equals(path, mapping.Value, StringComparison.OrdinalIgnoreCase))
                    {
                        return mapping.Key;
                    }

                    if (path.StartsWith(mapping.Value + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                        path.StartsWith(mapping.Value + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        return mapping.Key + path.Remove(0, mapping.Value.Length);
                    }
#else
                    if (string.Equals(path, mapping.Value))
                    {
                        return mapping.Key;
                    }

                    if (path.StartsWith(mapping.Value + Path.DirectorySeparatorChar))
                    {
                        return mapping.Key + path.Remove(0, mapping.Value.Length);
                    }
#endif
                }
            }

            return path;
        }

        private List<MountVolume> ParseVolumes(IList<string> volumes)
        {
            List<MountVolume> mountVolumes = new List<MountVolume>();
            if (volumes?.Count > 0)
            {
                foreach (var volume in volumes)
                {
                    var volumeSplit = volume.Split(":");
                    if (volumeSplit?.Length == 3)
                    {
                        // source:target:ro
                        mountVolumes.Add(new MountVolume(volumeSplit[0], volumeSplit[1], String.Equals(volumeSplit[2], "ro")));
                    }
                    else if (volumeSplit?.Length == 2)
                    {
                        if (String.Equals(volumeSplit[1], "ro"))
                        {
                            // target:ro
                            mountVolumes.Add(new MountVolume(null, volumeSplit[0], true));
                        }
                        else
                        {
                            // source:target
                            mountVolumes.Add(new MountVolume(volumeSplit[0], volumeSplit[1]));
                        }
                    }
                    else
                    {
                        // target - or, default to passing straight through
                        mountVolumes.Add(new MountVolume(null, volume));
                    }
                }
            }
            return mountVolumes;
        }

        private List<PortMapping> ParsePorts(IList<string> ports)
        {
            List<PortMapping> portMappings = new List<PortMapping>();
            if (ports?.Count > 0)
            {
                foreach (var port in ports)
                {
                    var protoSplit = port.Split("/");
                    String portString;
                    String protoString = null;
                    if (protoSplit?.Length == 2)
                    {
                        protoString = protoSplit[1];
                    }
                    portString = protoSplit[0];
                    var portSplit = portString.Split(":");
                    if (portSplit?.Length == 3)
                    {
                        // host:hostport:targetport
                        portMappings.Add(new PortMapping($"{portSplit[0]}:{portSplit[1]}", portSplit[2], protoString));
                    }
                    else if (portSplit?.Length == 2)
                    {
                        // hostport:targetport
                        portMappings.Add(new PortMapping(portSplit[0], portSplit[1], protoString));
                    }
                    else
                    {
                        // target - or, default to passing straight through
                        portMappings.Add(new PortMapping(null, port, protoString));
                    }
                }
            }
            return portMappings;
        }
    }

    public class MountVolume
    {
        public MountVolume(string sourceVolumePath, string targetVolumePath, bool readOnly = false)
        {
            this.SourceVolumePath = sourceVolumePath;
            this.TargetVolumePath = targetVolumePath;
            this.ReadOnly = readOnly;
        }

        public string SourceVolumePath { get; set; }
        public string TargetVolumePath { get; set; }
        public bool ReadOnly { get; set; }
    }

    public class PortMapping
    {
        public PortMapping(string hostPort, string containerPort, string protocol)
        {
            this.HostPort = hostPort;
            this.ContainerPort = containerPort;
            this.Protocol = protocol;
        }

        public string HostPort { get; set; }
        public string ContainerPort { get; set; }
        public string Protocol { get; set; }
    }

    public class DockerVersion
    {
        public DockerVersion(Version serverVersion, Version clientVersion)
        {
            this.ServerVersion = serverVersion;
            this.ClientVersion = clientVersion;
        }

        public Version ServerVersion { get; set; }
        public Version ClientVersion { get; set; }
    }
}