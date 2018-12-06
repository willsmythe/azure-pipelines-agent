// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using Agent.Plugins.TestResultParser.Client;
using Microsoft.TeamFoundation.TestManagement.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using TestRun = Agent.Plugins.TestResultParser.TestResult.TestRun;

namespace Agent.Plugins.TestResultParser.Publish
{
    public class PipelineTestRunPublisher : ITestRunPublisher
    {
        public PipelineTestRunPublisher(ClientFactory clientFactory, PipelineConfig pipelineConfig)
        {
            _pipelineConfig = pipelineConfig;
            _httpClient = clientFactory.GetClient<TestManagementHttpClient>();
        }

        public Task PublishAsync(TestRun testRun)
        {
            var r = new RunCreateModel(name: "Log parsed test run", buildId: _pipelineConfig.BuildId, state: TestRunState.InProgress.ToString(), isAutomated: true);
            var run = _httpClient.CreateTestRunAsync(r, _pipelineConfig.Project).SyncResult();

            var testResults = new List<TestCaseResult>();

            foreach (var passedTest in testRun.PassedTests)
            {
                testResults.Add(new TestCaseResult
                {
                    TestCaseTitle = passedTest.Name,
                    AutomatedTestName = passedTest.Name,
                    DurationInMs = passedTest.ExecutionTime.TotalMilliseconds,
                    State = "Completed",
                    AutomatedTestType = "LogParser",
                    Outcome = TestOutcome.Passed.ToString()

                });
            }

            foreach (var passedTest in testRun.FailedTests)
            {
                testResults.Add(new TestCaseResult
                {
                    TestCaseTitle = passedTest.Name,
                    AutomatedTestName = passedTest.Name,
                    DurationInMs = passedTest.ExecutionTime.TotalMilliseconds,
                    State = "Completed",
                    AutomatedTestType = "LogParser",
                    Outcome = TestOutcome.Failed.ToString()
                });
            }

            foreach (var passedTest in testRun.SkippedTests)
            {
                testResults.Add(new TestCaseResult
                {
                    TestCaseTitle = passedTest.Name,
                    AutomatedTestName = passedTest.Name,
                    DurationInMs = passedTest.ExecutionTime.TotalMilliseconds,
                    State = "Completed",
                    AutomatedTestType = "LogParser",
                    Outcome = TestOutcome.NotExecuted.ToString()

                });
            }

            var results = _httpClient.AddTestResultsToTestRunAsync(testResults.ToArray(), _pipelineConfig.Project, run.Id).SyncResult();

            return _httpClient.UpdateTestRunAsync(new RunUpdateModel(state: TestRunState.Completed.ToString()), _pipelineConfig.Project, run.Id);
        }

        private readonly TestManagementHttpClient _httpClient;
        private readonly PipelineConfig _pipelineConfig;
    }
}
