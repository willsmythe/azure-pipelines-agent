// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.TestRunManger
{
    using Agent.Plugins.TestResultParser.TestResult;

    /// <summary>
    /// Manages the test run
    /// </summary>
    public interface ITestRunManager
    {
        /// <summary>
        /// Validates and publishes the test run
        /// </summary>
        /// <param name="testRun"></param>
        void Publish(TestRun testRun);
    }
}