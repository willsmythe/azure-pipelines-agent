using Microsoft.TeamFoundation.DistributedTask.WebApi;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;
using System.Threading;
using System.Collections.ObjectModel;
using Pipelines = Microsoft.TeamFoundation.DistributedTask.Pipelines;
using Agent.Sdk;

namespace Microsoft.VisualStudio.Services.Agent.Tests.LogPluginHost
{
    public sealed class LogPluginHostL0
    {
        // private IExecutionContext _jobEc;
        // private JobRunner _jobRunner;
        // private List<IStep> _initResult = new List<IStep>();
        // private Pipelines.AgentJobRequestMessage _message;
        // private CancellationTokenSource _tokenSource;
        // private Mock<IJobServer> _jobServer;
        // private Mock<IJobServerQueue> _jobServerQueue;
        // private Mock<IVstsAgentWebProxy> _proxyConfig;
        // private Mock<IAgentCertificateManager> _cert;
        // private Mock<IConfigurationStore> _config;
        // private Mock<ITaskServer> _taskServer;
        // private Mock<IExtensionManager> _extensions;
        // private Mock<IStepsRunner> _stepRunner;

        // private Mock<IJobExtension> _jobExtension;
        // private Mock<IPagingLogger> _logger;
        // private Mock<ITempDirectoryManager> _temp;
        // private Mock<IDiagnosticLogManager> _diagnosticLogManager;

        // private TestHostContext CreateTestContext([CallerMemberName] String testName = "")
        // {
        //     var hc = new TestHostContext(this, testName);

        //     _jobEc = new Agent.Worker.ExecutionContext();
        //     _config = new Mock<IConfigurationStore>();
        //     _extensions = new Mock<IExtensionManager>();
        //     _jobExtension = new Mock<IJobExtension>();
        //     _jobServer = new Mock<IJobServer>();
        //     _jobServerQueue = new Mock<IJobServerQueue>();
        //     _proxyConfig = new Mock<IVstsAgentWebProxy>();
        //     _cert = new Mock<IAgentCertificateManager>();
        //     _taskServer = new Mock<ITaskServer>();
        //     _stepRunner = new Mock<IStepsRunner>();
        //     _logger = new Mock<IPagingLogger>();
        //     _temp = new Mock<ITempDirectoryManager>();
        //     _diagnosticLogManager = new Mock<IDiagnosticLogManager>();

        //     if (_tokenSource != null)
        //     {
        //         _tokenSource.Dispose();
        //         _tokenSource = null;
        //     }

        //     _tokenSource = new CancellationTokenSource();
        //     var expressionManager = new ExpressionManager();
        //     expressionManager.Initialize(hc);
        //     hc.SetSingleton<IExpressionManager>(expressionManager);

        //     _jobRunner = new JobRunner();
        //     _jobRunner.Initialize(hc);

        //     TaskOrchestrationPlanReference plan = new TaskOrchestrationPlanReference();
        //     TimelineReference timeline = new Timeline(Guid.NewGuid());
        //     JobEnvironment environment = new JobEnvironment();
        //     environment.Variables[Constants.Variables.System.Culture] = "en-US";
        //     environment.SystemConnection = new ServiceEndpoint()
        //     {
        //         Name = WellKnownServiceEndpointNames.SystemVssConnection,
        //         Url = new Uri("https://test.visualstudio.com"),
        //         Authorization = new EndpointAuthorization()
        //         {
        //             Scheme = "Test",
        //         }
        //     };
        //     environment.SystemConnection.Authorization.Parameters["AccessToken"] = "token";

        //     List<TaskInstance> tasks = new List<TaskInstance>();
        //     Guid JobId = Guid.NewGuid();
        //     _message = Pipelines.AgentJobRequestMessageUtil.Convert(new AgentJobRequestMessage(plan, timeline, JobId, testName, testName, environment, tasks));

        //     _extensions.Setup(x => x.GetExtensions<IJobExtension>()).
        //         Returns(new[] { _jobExtension.Object }.ToList());

        //     _initResult.Clear();

        //     _jobExtension.Setup(x => x.InitializeJob(It.IsAny<IExecutionContext>(), It.IsAny<Pipelines.AgentJobRequestMessage>())).
        //         Returns(Task.FromResult(_initResult));
        //     _jobExtension.Setup(x => x.HostType)
        //         .Returns<string>(null);

        //     _proxyConfig.Setup(x => x.ProxyAddress)
        //         .Returns(string.Empty);

        //     var settings = new AgentSettings
        //     {
        //         AgentId = 1,
        //         AgentName = "agent1",
        //         ServerUrl = "https://test.visualstudio.com",
        //         WorkFolder = "_work",
        //     };

        //     _config.Setup(x => x.GetSettings())
        //         .Returns(settings);

        //     _logger.Setup(x => x.Setup(It.IsAny<Guid>(), It.IsAny<Guid>()));

        //     hc.SetSingleton(_config.Object);
        //     hc.SetSingleton(_jobServer.Object);
        //     hc.SetSingleton(_jobServerQueue.Object);
        //     hc.SetSingleton(_proxyConfig.Object);
        //     hc.SetSingleton(_cert.Object);
        //     hc.SetSingleton(_taskServer.Object);
        //     hc.SetSingleton(_stepRunner.Object);
        //     hc.SetSingleton(_extensions.Object);
        //     hc.SetSingleton(_temp.Object);
        //     hc.SetSingleton(_diagnosticLogManager.Object);
        //     hc.EnqueueInstance<IExecutionContext>(_jobEc);
        //     hc.EnqueueInstance<IPagingLogger>(_logger.Object);
        //     return hc;
        // }

        public class TestTrace : IAgentLogPluginTrace
        {
            private Tracing _trace;
            public TestTrace(TestHostContext testHostContext)
            {
                _trace = testHostContext.GetTrace();
            }

            public List<string> Outputs = new List<string>();

            public void Output(string message)
            {
                Outputs.Add(message);
                _trace.Info(message);
            }

            public void Trace(string message)
            {
                Outputs.Add(message);
                _trace.Info(message);
            }
        }

        public class TestPlugin1 : IAgentLogPlugin
        {
            public string FriendlyName => "Test1";

            public Task FinalizeAsync(IAgentLogPluginContext context)
            {
                context.Output("Done");
                return Task.CompletedTask;
            }

            public Task<bool> InitializeAsync(IAgentLogPluginContext context)
            {
                return Task.FromResult(true);
            }

            public Task ProcessLineAsync(IAgentLogPluginContext context, Pipelines.TaskStepDefinitionReference step, string line)
            {
                context.Output(line);
                return Task.CompletedTask;
            }
        }

        public class TestPlugin2 : IAgentLogPlugin
        {
            public string FriendlyName => "Test2";

            public Task FinalizeAsync(IAgentLogPluginContext context)
            {
                context.Output("Done");
                return Task.CompletedTask;
            }

<<<<<<< HEAD
=======
            public Task<bool> InitializeAsync(IAgentLogPluginContext context)
            }

>>>>>>> f3f22d3a2bda22979747ce04f9ddd0dd9afeefaf
            public Task ProcessLineAsync(IAgentLogPluginContext context, Pipelines.TaskStepDefinitionReference step, string line)
            {
                return Task.CompletedTask;
            }
        }

        public class TestPluginSlow : IAgentLogPlugin
        {
            public string FriendlyName => "TestSlow";

            public Task FinalizeAsync(IAgentLogPluginContext context)
            {
                context.Output("Done");
                return Task.CompletedTask;
            }

            public Task<bool> InitializeAsync(IAgentLogPluginContext context)
            {
                return Task.FromResult(true);
            }

            public async Task ProcessLineAsync(IAgentLogPluginContext context, Pipelines.TaskStepDefinitionReference step, string line)
            {
                context.Output("BLOCK");
                await Task.Delay(-1);
            }
        }

        public class TestPluginSlowRecover : IAgentLogPlugin
        {
            private int _counter = 0;
            public string FriendlyName => "TestSlowRecover";

            public Task FinalizeAsync(IAgentLogPluginContext context)
            {
                context.Output("Done");
                return Task.CompletedTask;
            }

            public Task<bool> InitializeAsync(IAgentLogPluginContext context)
            {
                return Task.FromResult(true);
            }

            public async Task ProcessLineAsync(IAgentLogPluginContext context, Pipelines.TaskStepDefinitionReference step, string line)
            {
                if (_counter++ < 1)
                {
                    context.Output("SLOW");
                    await Task.Delay(500);
                }
                else
                {
                    context.Output(line);
                }
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Plugin")]
        public async Task LogPluginHost_RunSinglePlugin()
        {
            using (TestHostContext tc = new TestHostContext(this))
            {
                AgentLogPluginHostContext hostContext = CreateTestLogPluginHostContext();
                List<IAgentLogPlugin> plugins = new List<IAgentLogPlugin>() { new TestPlugin1() };

                TestTrace trace = new TestTrace(tc);
                AgentLogPluginHost logPluginHost = new AgentLogPluginHost(hostContext, plugins, trace);
                var task = logPluginHost.Run();
                for (int i = 0; i < 1000; i++)
                {
                    logPluginHost.EnqueueOutput($"{Guid.Empty.ToString("D")}:{i}");
                }

                await Task.Delay(1000);
                logPluginHost.Finish();
                await task;

                Assert.True(trace.Outputs.Contains("Test1: 0"));
                Assert.True(trace.Outputs.Contains("Test1: 999"));
                Assert.True(trace.Outputs.Contains("Test1: Done"));
                Assert.True(trace.Outputs.Exists(x => x.Contains("Test1: Pending process")));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Plugin")]
        public async Task LogPluginHost_RunMultiplePlugins()
        {
            using (TestHostContext tc = new TestHostContext(this))
            {
                AgentLogPluginHostContext hostContext = CreateTestLogPluginHostContext();
                List<IAgentLogPlugin> plugins = new List<IAgentLogPlugin>() { new TestPlugin1(), new TestPlugin2() };

                TestTrace trace = new TestTrace(tc);
                AgentLogPluginHost logPluginHost = new AgentLogPluginHost(hostContext, plugins, trace);
                var task = logPluginHost.Run();
                for (int i = 0; i < 1000; i++)
                {
                    logPluginHost.EnqueueOutput($"{Guid.Empty.ToString("D")}:{i}");
                }

                await Task.Delay(1000);
                logPluginHost.Finish();
                await task;

                Assert.True(trace.Outputs.Contains("Test1: 0"));
                Assert.True(trace.Outputs.Contains("Test1: 999"));
                Assert.True(trace.Outputs.Contains("Test1: Done"));
                Assert.True(trace.Outputs.Exists(x => x.Contains("Test1: Pending process")));
                Assert.True(trace.Outputs.Contains("Test2: 0"));
                Assert.True(trace.Outputs.Contains("Test2: 999"));
                Assert.True(trace.Outputs.Contains("Test2: Done"));
                Assert.True(trace.Outputs.Exists(x => x.Contains("Test2: Pending process")));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Plugin")]
        public async Task LogPluginHost_ShortCircuitSlowPlugin()
        {
            using (TestHostContext tc = new TestHostContext(this))
            {
                AgentLogPluginHostContext hostContext = CreateTestLogPluginHostContext();
                List<IAgentLogPlugin> plugins = new List<IAgentLogPlugin>() { new TestPlugin1(), new TestPluginSlow() };

                TestTrace trace = new TestTrace(tc);
                AgentLogPluginHost logPluginHost = new AgentLogPluginHost(hostContext, plugins, trace, 100, 100);
                var task = logPluginHost.Run();
                for (int i = 0; i < 1000; i++)
                {
                    logPluginHost.EnqueueOutput($"{Guid.Empty.ToString("D")}:{i}");
                }

                await Task.Delay(1000);
                logPluginHost.Finish();
                await task;

                // regular one still running
                Assert.True(trace.Outputs.Contains("Test1: 0"));
                Assert.True(trace.Outputs.Contains("Test1: 999"));
                Assert.True(trace.Outputs.Contains("Test1: Done"));
                Assert.True(trace.Outputs.Exists(x => x.Contains("Test1: Pending process")));

                // slow one got killed
                Assert.False(trace.Outputs.Contains("TestSlow: Done"));
                Assert.True(trace.Outputs.Exists(x => x.Contains("Plugin 'TestSlow' has been short circuited")));
            }
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Plugin")]
        public async Task LogPluginHost_SlowPluginRecover()
        {
            using (TestHostContext tc = new TestHostContext(this))
            {
                AgentLogPluginHostContext hostContext = CreateTestLogPluginHostContext();
                List<IAgentLogPlugin> plugins = new List<IAgentLogPlugin>() { new TestPlugin1(), new TestPluginSlowRecover() };

                TestTrace trace = new TestTrace(tc);
                AgentLogPluginHost logPluginHost = new AgentLogPluginHost(hostContext, plugins, trace, 950, 100);
                var task = logPluginHost.Run();
                for (int i = 0; i < 1000; i++)
                {
                    logPluginHost.EnqueueOutput($"{Guid.Empty.ToString("D")}:{i}");
                }

                await Task.Delay(1000);
                logPluginHost.Finish();
                await task;

                // regular one still running
                Assert.True(trace.Outputs.Contains("Test1: 0"));
                Assert.True(trace.Outputs.Contains("Test1: 999"));
                Assert.True(trace.Outputs.Contains("Test1: Done"));
                Assert.True(trace.Outputs.Exists(x => x.Contains("Test1: Pending process")));

                Assert.True(trace.Outputs.Contains("TestSlowRecover: Done"));
                Assert.True(trace.Outputs.Exists(x => x.Contains("TestPluginSlowRecover' has too many buffered outputs.")));
                Assert.True(trace.Outputs.Exists(x => x.Contains("TestPluginSlowRecover' has cleared out buffered outputs.")));
            }
        }

        private AgentLogPluginHostContext CreateTestLogPluginHostContext()
        {
            AgentLogPluginHostContext hostContext = new AgentLogPluginHostContext()
            {
                Endpoints = new List<ServiceEndpoint>(),
                PluginAssemblies = new List<string>(),
                Repositories = new List<Pipelines.RepositoryResource>(),
                Variables = new Dictionary<string, VariableValue>(),
                Steps = new Dictionary<string, Pipelines.TaskStepDefinitionReference>()
            };

            hostContext.Steps[Guid.Empty.ToString("D")] = new Pipelines.TaskStepDefinitionReference()
            {
                Id = Guid.NewGuid(),
                Name = "Test",
                Version = "1.0.0."
            };

            var systemConnection = new ServiceEndpoint()
            {
                Name = WellKnownServiceEndpointNames.SystemVssConnection,
                Id = Guid.NewGuid(),
                Url = new Uri("https://dev.azure.com/test"),
                Authorization = new EndpointAuthorization()
                {
                    Scheme = EndpointAuthorizationSchemes.OAuth,
                    Parameters = { { EndpointAuthorizationParameters.AccessToken, "Test" } }
                }
            };

            hostContext.Endpoints.Add(systemConnection);

            return hostContext;
        }
    }
}
