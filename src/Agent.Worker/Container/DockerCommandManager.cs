using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Services.Agent.Util;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Container
{
    [ServiceLocator(Default = typeof(DockerCommandManager))]
    public interface IDockerCommandManager : IAgentService
    {
        string DockerPath { get; }
        Task<DockerVersion> DockerVersion(IExecutionContext context);
        Task<int> DockerLogin(IExecutionContext context, string server, string username, string password);
        Task<int> DockerLogout(IExecutionContext context, string server);
        Task<int> DockerPull(IExecutionContext context, string image);
        Task<string> DockerCreate(IExecutionContext context, ContainerInfo container);
        Task<string> DockerCreate(IExecutionContext context, string displayName, string image, List<MountVolume> mountVolumes, string network, string options, IDictionary<string, string> environment, string command);
        Task<int> DockerStart(IExecutionContext context, string containerId);
        Task<int> DockerStop(IExecutionContext context, string containerId);
        Task<int> DockerLogs(IExecutionContext context, string containerId);
        Task<List<string>> DockerPS(IExecutionContext context, string containerId, string filter);
        Task<int> DockerRemove(IExecutionContext context, string containerId);
        Task<int> DockerNetworkCreate(IExecutionContext context, string network);
        Task<int> DockerNetworkRemove(IExecutionContext context, string network);
        Task<int> DockerExec(IExecutionContext context, string containerId, string options, string command);
        Task<int> DockerExec(IExecutionContext context, string containerId, string options, string command, List<string> outputs);
        Task<List<PortMapping>> DockerPort(IExecutionContext context, string containerId);
    }

    public class DockerCommandManager : AgentService, IDockerCommandManager
    {
        public string DockerPath { get; private set; }

        public override void Initialize(IHostContext hostContext)
        {
            base.Initialize(hostContext);
            DockerPath = WhichUtil.Which("docker", true, Trace);
        }

        public async Task<DockerVersion> DockerVersion(IExecutionContext context)
        {
            string serverVersionStr = (await ExecuteDockerCommandAsync(context, "version", "--format '{{.Server.Version}}'")).FirstOrDefault();
            ArgUtil.NotNullOrEmpty(serverVersionStr, "Docker.Server.Version");
            context.Output($"Docker daemon version: {serverVersionStr}");

            string clientVersionStr = (await ExecuteDockerCommandAsync(context, "version", "--format '{{.Client.Version}}'")).FirstOrDefault();
            ArgUtil.NotNullOrEmpty(serverVersionStr, "Docker.Client.Version");
            context.Output($"Docker client version: {clientVersionStr}");

            // we interested about major.minor.patch version
            Regex verRegex = new Regex("\\d+\\.\\d+(\\.\\d+)?", RegexOptions.IgnoreCase);

            Version serverVersion = null;
            var serverVersionMatchResult = verRegex.Match(serverVersionStr);
            if (serverVersionMatchResult.Success && !string.IsNullOrEmpty(serverVersionMatchResult.Value))
            {
                if (!Version.TryParse(serverVersionMatchResult.Value, out serverVersion))
                {
                    serverVersion = null;
                }
            }

            Version clientVersion = null;
            var clientVersionMatchResult = verRegex.Match(serverVersionStr);
            if (clientVersionMatchResult.Success && !string.IsNullOrEmpty(clientVersionMatchResult.Value))
            {
                if (!Version.TryParse(clientVersionMatchResult.Value, out clientVersion))
                {
                    clientVersion = null;
                }
            }

            return new DockerVersion(serverVersion, clientVersion);
        }

        public async Task<int> DockerLogin(IExecutionContext context, string server, string username, string password)
        {
#if OS_WINDOWS
            // Wait for 17.07 to switch using stdin for docker registry password.
            return await ExecuteDockerCommandAsync(context, "login", $"--username \"{username}\" --password \"{password.Replace("\"", "\\\"")}\" {server}", new List<string>() { password }, context.CancellationToken);
#else
            return await ExecuteDockerCommandAsync(context, "login", $"--username \"{username}\" --password-stdin {server}", new List<string>() { password }, context.CancellationToken);
#endif
        }

        public async Task<int> DockerLogout(IExecutionContext context, string server)
        {
            return await ExecuteDockerCommandAsync(context, "logout", $"{server}", context.CancellationToken);
        }

        public async Task<int> DockerPull(IExecutionContext context, string image)
        {
            return await ExecuteDockerCommandAsync(context, "pull", image, context.CancellationToken);
        }

        public async Task<string> DockerCreate(IExecutionContext context, ContainerInfo container)
        {
            IList<string> dockerOptions = new List<string>();
            dockerOptions.Add($"--name {container.ContainerDisplayName}");
            if (!string.IsNullOrEmpty(container.ContainerNetwork))
            {
                dockerOptions.Add($"--network {container.ContainerNetwork}");
            }
            if (!string.IsNullOrEmpty(container.ContainerNetworkAlias))
            {
                dockerOptions.Add($"--network-alias {container.ContainerNetworkAlias}");
            }
            foreach (var port in container.PortMappings)
            {
                var portArg = string.Empty;
                if (!string.IsNullOrEmpty(port.HostPort) && !string.IsNullOrEmpty(port.ContainerPort))
                {
                    portArg = $"-p {port.HostPort}:{port.ContainerPort}";

                }
                else if (string.IsNullOrEmpty(port.HostPort) && !string.IsNullOrEmpty(port.ContainerPort))
                {
                    portArg = $"-p {port.ContainerPort}";
                }
                if (!string.IsNullOrEmpty(portArg))
                {
                    dockerOptions.Add(portArg);
                }
            }
            dockerOptions.Add($"{container.ContainerCreateOptions}");
            foreach (var env in container.ContainerEnvironmentVariables)
            {
                dockerOptions.Add($"-e \"{env.Key}={env.Value.Replace("\"", "\\\"")}\"");
            }
            foreach (var volume in container.MountVolumes)
            {
                // replace `"` with `\"` and add `"{0}"` to all path.
                var volumeArg = $"-v \"{volume.SourceVolumePath.Replace("\"", "\\\"")}\":\"{volume.TargetVolumePath.Replace("\"", "\\\"")}\"";
                if (volume.ReadOnly)
                {
                    volumeArg += ":ro";
                }
                dockerOptions.Add(volumeArg);
            }
            dockerOptions.Add($"{container.ContainerImage}");
            dockerOptions.Add($"{container.ContainerCommand}");

            var optionsString = string.Join(" ", dockerOptions);
            List<string> outputStrings = await ExecuteDockerCommandAsync(context, "create", optionsString);
            return outputStrings.FirstOrDefault();
        }

        public async Task<string> DockerCreate(IExecutionContext context, string displayName, string image, List<MountVolume> mountVolumes, string network, string options, IDictionary<string, string> environment, string command)
        {
            string dockerMountVolumesArgs = string.Empty;
            if (mountVolumes?.Count > 0)
            {
                foreach (var volume in mountVolumes)
                {
                    // replace `"` with `\"` and add `"{0}"` to all path.
                    dockerMountVolumesArgs += $" -v \"{volume.SourceVolumePath.Replace("\"", "\\\"")}\":\"{volume.TargetVolumePath.Replace("\"", "\\\"")}\"";
                    if (volume.ReadOnly)
                    {
                        dockerMountVolumesArgs += ":ro";
                    }
                }
            }

            string dockerEnvArgs = string.Empty;
            if (environment?.Count > 0)
            {
                foreach (var env in environment)
                {
                    dockerEnvArgs += $" -e \"{env.Key}={env.Value.Replace("\"", "\\\"")}\"";
                }
            }

            IList<string> networkAliases = new List<string>();
            string dockerNetworkAliasArgs = string.Empty;
            if (networkAliases?.Count > 0)
            {
                foreach (var alias in networkAliases)
                {
                    dockerNetworkAliasArgs += $" --network-alias {alias}";
                }
            }

            IList<string> ports = new List<string>();
            string dockerPortArgs = string.Empty;
            if (ports?.Count > 0)
            {
                foreach(var port in ports)
                {
                    dockerPortArgs += $" -p {port}";
                }
            }

#if OS_WINDOWS
            string dockerArgs = $"--name {displayName} {options} {dockerEnvArgs} {dockerMountVolumesArgs} {image} {command}";  // add --network={network} and -v '\\.\pipe\docker_engine:\\.\pipe\docker_engine' when they are available (17.09)
#else
            string dockerArgs = $"--name {displayName} --network={network} {dockerNetworkAliasArgs} {dockerPortArgs} -v /var/run/docker.sock:/var/run/docker.sock {options} {dockerEnvArgs} {dockerMountVolumesArgs} {image} {command}";
#endif
            List<string> outputStrings = await ExecuteDockerCommandAsync(context, "create", dockerArgs);
            return outputStrings.FirstOrDefault();
        }

        public async Task<int> DockerStart(IExecutionContext context, string containerId)
        {
            return await ExecuteDockerCommandAsync(context, "start", containerId, context.CancellationToken);
        }

        public async Task<int> DockerStop(IExecutionContext context, string containerId)
        {
            return await ExecuteDockerCommandAsync(context, "stop", containerId, context.CancellationToken);
        }

        public async Task<int> DockerRemove(IExecutionContext context, string containerId)
        {
            return await ExecuteDockerCommandAsync(context, "rm", containerId, context.CancellationToken);
        }

        public async Task<int> DockerLogs(IExecutionContext context, string containerId)
        {
            return await ExecuteDockerCommandAsync(context, "logs", $"--details {containerId}", context.CancellationToken);
        }

        public async Task<List<string>> DockerPS(IExecutionContext context, string containerId, string filter)
        {
            return await ExecuteDockerCommandAsync(context, "ps", $"--all --filter id={containerId} {filter} --no-trunc --format \"{{{{.ID}}}} {{{{.Status}}}}\"");
        }

        public async Task<int> DockerNetworkCreate(IExecutionContext context, string network)
        {
#if OS_WINDOWS
            return await ExecuteDockerCommandAsync(context, "network", $"create {network} --driver nat", context.CancellationToken);
#else
            return await ExecuteDockerCommandAsync(context, "network", $"create {network}", context.CancellationToken);
#endif
        }

        public async Task<int> DockerNetworkRemove(IExecutionContext context, string network)
        {
            return await ExecuteDockerCommandAsync(context, "network", $"rm {network}", context.CancellationToken);
        }

        public async Task<int> DockerExec(IExecutionContext context, string containerId, string options, string command)
        {
            return await ExecuteDockerCommandAsync(context, "exec", $"{options} {containerId} {command}", context.CancellationToken);
        }

        public async Task<int> DockerExec(IExecutionContext context, string containerId, string options, string command, List<string> output)
        {
            ArgUtil.NotNull(output, nameof(output));

            string arg = $"exec {options} {containerId} {command}".Trim();
            context.Command($"{DockerPath} {arg}");

            object outputLock = new object();
            var processInvoker = HostContext.CreateService<IProcessInvoker>();
            processInvoker.OutputDataReceived += delegate (object sender, ProcessDataReceivedEventArgs message)
            {
                if (!string.IsNullOrEmpty(message.Data))
                {
                    lock (outputLock)
                    {
                        output.Add(message.Data);
                    }
                }
            };

            processInvoker.ErrorDataReceived += delegate (object sender, ProcessDataReceivedEventArgs message)
            {
                if (!string.IsNullOrEmpty(message.Data))
                {
                    lock (outputLock)
                    {
                        output.Add(message.Data);
                    }
                }
            };

            return await processInvoker.ExecuteAsync(
                            workingDirectory: HostContext.GetDirectory(WellKnownDirectory.Work),
                            fileName: DockerPath,
                            arguments: arg,
                            environment: null,
                            requireExitCodeZero: false,
                            outputEncoding: null,
                            cancellationToken: CancellationToken.None);
        }

        public async Task<List<PortMapping>> DockerPort(IExecutionContext context, string containerId)
        {
            const string targetPort = "targetPort";
            const string proto = "proto";
            const string host = "host";
            const string hostPort = "hostPort";
            Regex rx = new Regex(
                //"TARGET_PORT/PROTO -> HOST:HOST_PORT"
                $"^(?<{targetPort}>\\d+)/(?<{proto}>\\w+) -> (?<{host}>.+):(?<{hostPort}>\\d+)$",
                RegexOptions.None,
                TimeSpan.FromMilliseconds(100)
            );
            List<string> portMappingLines = await ExecuteDockerCommandAsync(context, "port", containerId);
            List<PortMapping> portMappings = new List<PortMapping>();
            foreach(var line in portMappingLines)
            {
                Match m = rx.Match(line);
                if (m.Success)
                {
                    portMappings.Add(new PortMapping(
                        m.Groups[hostPort].Value,
                        m.Groups[targetPort].Value,
                        m.Groups[proto].Value
                    ));
                }
            }
            return portMappings;
        }

        private Task<int> ExecuteDockerCommandAsync(IExecutionContext context, string command, string options, CancellationToken cancellationToken = default(CancellationToken))
        {
            return ExecuteDockerCommandAsync(context, command, options, null, cancellationToken);
        }

        private async Task<int> ExecuteDockerCommandAsync(IExecutionContext context, string command, string options, IList<string> standardIns = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            string arg = $"{command} {options}".Trim();
            context.Command($"{DockerPath} {arg}");

            var processInvoker = HostContext.CreateService<IProcessInvoker>();
            processInvoker.OutputDataReceived += delegate (object sender, ProcessDataReceivedEventArgs message)
            {
                context.Output(message.Data);
            };

            processInvoker.ErrorDataReceived += delegate (object sender, ProcessDataReceivedEventArgs message)
            {
                context.Output(message.Data);
            };

            return await processInvoker.ExecuteAsync(
                workingDirectory: HostContext.GetDirectory(WellKnownDirectory.Work),
                fileName: DockerPath,
                arguments: arg,
                environment: null,
                requireExitCodeZero: false,
                outputEncoding: null,
                killProcessOnCancel: false,
                contentsToStandardIn: standardIns,
                cancellationToken: cancellationToken);
        }

        private async Task<List<string>> ExecuteDockerCommandAsync(IExecutionContext context, string command, string options)
        {
            string arg = $"{command} {options}".Trim();
            context.Command($"{DockerPath} {arg}");

            List<string> output = new List<string>();
            var processInvoker = HostContext.CreateService<IProcessInvoker>();
            processInvoker.OutputDataReceived += delegate (object sender, ProcessDataReceivedEventArgs message)
            {
                if (!string.IsNullOrEmpty(message.Data))
                {
                    output.Add(message.Data);
                    context.Output(message.Data);
                }
            };

            processInvoker.ErrorDataReceived += delegate (object sender, ProcessDataReceivedEventArgs message)
            {
                if (!string.IsNullOrEmpty(message.Data))
                {
                    context.Output(message.Data);
                }
            };

            await processInvoker.ExecuteAsync(
                            workingDirectory: HostContext.GetDirectory(WellKnownDirectory.Work),
                            fileName: DockerPath,
                            arguments: arg,
                            environment: null,
                            requireExitCodeZero: true,
                            outputEncoding: null,
                            cancellationToken: CancellationToken.None);

            return output;
        }
    }
}