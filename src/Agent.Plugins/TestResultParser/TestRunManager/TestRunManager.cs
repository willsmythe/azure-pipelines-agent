// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Agent.Plugins.TestResultParser.Loggers;
using Agent.Plugins.TestResultParser.Publish;
using Agent.Plugins.TestResultParser.TestResult;

namespace Agent.Plugins.TestResultParser.TestRunManger
{

    /// <inheritdoc/>
    public class TestRunManager : ITestRunManager
    {
        private readonly ITestRunPublisher _publisher;

        /// <summary>
        /// Construct the TestRunManger
        /// </summary>
        /// <param name="testRunPublisher"></param>
        public TestRunManager(ITestRunPublisher testRunPublisher)
        {
            _publisher = testRunPublisher;
        }

        /// <inheritdoc/>
        public void Publish(TestRun testRun)
        {
            var validatedTestRun = this.ValidateAndPrepareForPublish(testRun);
            if (validatedTestRun != null)
            {
                _publisher.PublishAsync(validatedTestRun); //TODO fix this
            }
        }

        private TestRun ValidateAndPrepareForPublish(TestRun testRun)
        {
            if (testRun?.TestRunSummary == null)
            {
                TraceLogger.Error("TestRunManger.ValidateAndPrepareForPublish : TestRun or TestRunSummary is null.");
                return null;
            }

            // TotalTests count should always be less than passed and failed test count combined
            if (testRun.TestRunSummary.TotalTests < testRun.TestRunSummary.TotalFailed + testRun.TestRunSummary.TotalPassed)
            {
                testRun.TestRunSummary.TotalTests = testRun.TestRunSummary.TotalFailed + testRun.TestRunSummary.TotalPassed + testRun.TestRunSummary.TotalSkipped;
            }

            // Match the passed test count and clear the passed tests collection if mismatch occurs
            if (testRun.TestRunSummary.TotalPassed != testRun.PassedTests?.Count)
            {
                TraceLogger.Warning("TestRunManger.ValidateAndPrepareForPublish : Passed test count does not match the Test summary.");
                testRun.PassedTests = null;
            }

            // Match the failed test count and clear the failed tests collection if mismatch occurs
            if (testRun.TestRunSummary.TotalFailed != testRun.FailedTests?.Count)
            {
                TraceLogger.Warning("TestRunManger.ValidateAndPrepareForPublish : Failed test count does not match the Test summary.");
                testRun.FailedTests = null;
            }

            return testRun;
        }
    }
}
