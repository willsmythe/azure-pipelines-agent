// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Agent.Plugins.TestResultParser.Parser.Python
{
    using System;
    using System.Collections.Generic;
    using Parser;
    using TestResult;
    using TestRunManger;

    /// <summary>
    /// Python test result parser.
    /// </summary>
    public class PythonTestResultParser : AbstractTestResultParser
    {
        private ParserState _state;
        private TestResult _partialTestResult;
        private TestRun _currentTestRun;
        private int _currentTestRunId = 1;

        public sealed override string Name => "Python";
        public sealed override string Version => "1.0";

        /// <summary>
        /// Default constructor accepting only test run manager instance, rest of the requirements assume default values
        /// </summary>
        /// <param name="testRunManager"></param>
        public PythonTestResultParser(ITestRunManager testRunManager) : base(testRunManager)
        {
            Logger.Info("PythonTestResultParser : Starting python test result parser.");
            Telemetry.AddToCumulativeTelemetry(TelemetryConstants.EventArea, TelemetryConstants.Initialize, true);

            _state = ParserState.ExpectingTestResults;
            _currentTestRun = new TestRun($"{Name}/{Version}", _currentTestRunId);
        }

        /// <summary>
        /// Parses input data to detect python test result.
        /// </summary>
        /// <param name="logData">Data to be parsed.</param>
        public override void Parse(LogData logData)
        {
            // Validate data input
            if (!IsValidInput(logData.Message) || string.IsNullOrWhiteSpace(logData.Message)) return;

            try
            {
                switch (_state)
                {
                    case ParserState.ExpectingSummary:

                        // Summary Test count and total time should have already been parsed
                        // Try to parse test outcome, number of tests for each outcome
                        if (TryParseSummaryOutcome(logData))
                        {
                            PublishAndReset();
                            return;
                        }

                        // Summary was not parsed, reset the parser and try parse again.
                        Reset();
                        Parse(logData);
                        break;

                    case ParserState.ExpectingFailedResults:

                        // Try to parse for failed results and summary
                        // If summary is parsed, change the state
                        if (TryParseForFailedResult(logData)) return;
                        if (TryParseSummaryTestAndTime(logData))
                        {
                            _state = ParserState.ExpectingSummary;
                            return;
                        }

                        // Not expected, as Summary has not been encountered yet
                        // If a new TestResult is found, reset the parser and Parse again
                        if (TryParseTestResult(logData))
                        {
                            Logger.Error("PythonTestResultParser:Parse Expecting failed result or summary but found new test result.");
                            Telemetry.AddToCumulativeTelemetry(TelemetryConstants.EventArea, TelemetryConstants.SummaryOrFailedTestsNotFound, new List<int> { _currentTestRunId }, true);
                            Reset();
                            Parse(logData);
                        }
                        break;

                    case ParserState.ExpectingTestResults:
                    default:
                        if (TryParseTestResult(logData))
                        {
                            return;
                        }

                        // Change the state and clear the partial result if failed result or summary is found
                        if (TryParseForFailedResult(logData))
                        {
                            _partialTestResult = null;
                            _state = ParserState.ExpectingFailedResults;
                            return;
                        }
                        if (TryParseSummaryTestAndTime(logData))
                        {
                            _partialTestResult = null;
                            _state = ParserState.ExpectingSummary;
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"PythonTestResultParser.Parse : Unable to parse the log line {logData.Message} with exception {ex}");
                Telemetry.AddToCumulativeTelemetry(TelemetryConstants.EventArea, TelemetryConstants.ParseException, ex.Message);

                Reset();
            }
        }

        /// <summary>
        /// Reset the parser to original state
        /// </summary>
        private void Reset()
        {
            Logger.Info("PythonTestResultParser.Reset");
            _partialTestResult = null;
            _currentTestRun = new TestRun($"{Name}/{Version}", ++_currentTestRunId);
            _state = ParserState.ExpectingTestResults;
        }

        /// <summary>
        /// Publish the current test run and reset the parser
        /// </summary>
        private void PublishAndReset()
        {
            Logger.Info($"PythonTestResultParser:PublishAndReset : Publishing TestRun {_currentTestRunId}");
            TestRunManager.Publish(_currentTestRun);
            Reset();
        }

        private bool TryParseTestResult(LogData logData)
        {
            var resultMatch = PythonRegularExpressions.TestResult.Match(logData.Message);

            if (!resultMatch.Success)
            {
                return _partialTestResult != null && TryParseForPartialResult(logData);
            }

            _partialTestResult = null;

            var testCaseNameIdentifier = resultMatch.Groups[RegexCaptureGroups.TestCaseName].Value.Trim();
            string testCaseName = GetResultName(logData, testCaseNameIdentifier);

            if (testCaseName == null) return false;

            var result = new TestResult() { Name = testCaseName };

            // Determine the outcome of the Test result
            var testOutcomeIdentifier = resultMatch.Groups[RegexCaptureGroups.TestOutcome].Value.Trim();
            var passedResultMatch = PythonRegularExpressions.PassedOutcome.Match(testOutcomeIdentifier);
            if (passedResultMatch.Success)
            {
                result.Outcome = TestOutcome.Passed;
                _currentTestRun.PassedTests.Add(result);
                return true;
            }

            var skippedResultMatch = PythonRegularExpressions.SkippedOutcome.Match(testOutcomeIdentifier);
            if (skippedResultMatch.Success)
            {
                result.Outcome = TestOutcome.Skipped;
                _currentTestRun.SkippedTests.Add(result);
                return true;
            }

            // The outcome for this result could not be determined, adding to partial result
            _partialTestResult = result;
            return true;
        }

        private bool TryParseForPartialResult(LogData logData)
        {
            var partialResultMatch = PythonRegularExpressions.PassedOutcome.Match(logData.Message);
            if (partialResultMatch.Success)
            {
                _partialTestResult.Outcome = TestOutcome.Passed;
                _currentTestRun.PassedTests.Add(_partialTestResult);
                return true;
            }
            return false;
        }

        private bool TryParseForFailedResult(LogData logData)
        {
            // Parse
            var failedResultMatch = PythonRegularExpressions.FailedResult.Match(logData.Message);
            if (!failedResultMatch.Success) { return false; }

            // Set result name.
            string resultNameIdentifier = failedResultMatch.Groups[RegexCaptureGroups.TestCaseName].Value.Trim();

            var result = new TestResult();
            result.Name = GetResultName(logData, resultNameIdentifier);
            result.Outcome = TestOutcome.Failed;

            _currentTestRun.FailedTests.Add(result);
            return true;
        }

        private string GetResultName(LogData logData, string testResultNameIdentifier)
        {
            if (string.IsNullOrWhiteSpace(testResultNameIdentifier))
            {
                Logger.Verbose($"Test result name is null or whitespace in logData: {logData.Message}");
                return null;
            }

            return testResultNameIdentifier;
        }

        private bool TryParseSummaryTestAndTime(LogData logData)
        {
            var countAndTimeSummaryMatch = PythonRegularExpressions.TestCountAndTimeSummary.Match(logData.Message);
            if (countAndTimeSummaryMatch.Success)
            {
                var testcount = int.Parse(countAndTimeSummaryMatch.Groups[RegexCaptureGroups.TotalTests].Value);
                var secTime = int.Parse(countAndTimeSummaryMatch.Groups[RegexCaptureGroups.TestRunTime].Value);
                var msTime = int.Parse(countAndTimeSummaryMatch.Groups[RegexCaptureGroups.TestRunTimeMs].Value);

                _currentTestRun.TestRunSummary = new TestRunSummary
                {
                    TotalExecutionTime = new TimeSpan(0, 0, 0, secTime, msTime),
                    TotalTests = testcount
                };
                Logger.Info("PythonTestResultParser:TryParseSummaryTestAndTime : TestRunSummary with total time and tests created.");
                return true;
            }

            return false;
        }

        private bool TryParseSummaryOutcome(LogData logData)
        {
            if (_currentTestRun.TestRunSummary == null)
            {
                // This is safe check, if must be true always because parsers will try to parse for Outcome if Test and Time Summary already parsed.
                Logger.Error("PythonTestResultParser:TryParseSummaryOutcome : TestRunSummary is null");
                Telemetry.AddToCumulativeTelemetry(TelemetryConstants.EventArea, TelemetryConstants.TestRunSummaryCorrupted, new List<int> { _currentTestRunId }, true);
                return false;
            }

            var resultSummaryMatch = PythonRegularExpressions.TestOutcomeSummary.Match(logData.Message);
            if (resultSummaryMatch.Success)
            {
                var resultIdentifier = resultSummaryMatch.Groups[RegexCaptureGroups.TestOutcome].Value;

                var failureCountPatternMatch = PythonRegularExpressions.SummaryFailure.Match(resultIdentifier);
                if (failureCountPatternMatch.Success)
                {
                    _currentTestRun.TestRunSummary.TotalFailed = int.Parse(failureCountPatternMatch.Groups[RegexCaptureGroups.FailedTests].Value);
                }

                // TODO: We should have a separate bucket for errors
                var errorCountPatternMatch = PythonRegularExpressions.SummaryErrors.Match(resultIdentifier);
                if (errorCountPatternMatch.Success)
                {
                    _currentTestRun.TestRunSummary.TotalFailed += int.Parse(errorCountPatternMatch.Groups[RegexCaptureGroups.Errors].Value);
                }

                var skippedCountPatternMatch = PythonRegularExpressions.SummarySkipped.Match(resultIdentifier);
                if (skippedCountPatternMatch.Success)
                {
                    _currentTestRun.TestRunSummary.TotalSkipped = int.Parse(skippedCountPatternMatch.Groups[RegexCaptureGroups.SkippedTests].Value);
                }

                // Since total passed count is not available, calculate the count based on available statistics.
                _currentTestRun.TestRunSummary.TotalPassed = _currentTestRun.TestRunSummary.TotalTests - (_currentTestRun.TestRunSummary.TotalFailed + _currentTestRun.TestRunSummary.TotalSkipped);
                return true;
            }

            Logger.Error("PythonTestResultParser:TryParseSummaryOutcome : Expected match for SummaryTestOutcome was not found");
            Telemetry.AddToCumulativeTelemetry(TelemetryConstants.EventArea, TelemetryConstants.TestOutcomeSummaryNotFound, new List<int> { _currentTestRunId }, true);
            return false;
        }

        /// <summary>
        /// Validate the input data
        /// </summary>
        /// <param name="data">Log line that was passed to the parser</param>
        /// <returns>True if valid</returns>
        private bool IsValidInput(string data)
        {
            if (data == null)
            {
                Logger.Error("PythonTestResultParser.IsValidInput : Received null data");
                Telemetry.AddToCumulativeTelemetry(TelemetryConstants.EventArea, TelemetryConstants.InvalidInput, new List<int> { _currentTestRunId }, true);
            }

            return data != null;
        }
    }
}
