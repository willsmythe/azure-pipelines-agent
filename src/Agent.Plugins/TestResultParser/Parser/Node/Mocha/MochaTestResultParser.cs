// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Agent.Plugins.TestResultParser.TestResult;
using Agent.Plugins.TestResultParser.TestRunManger;

namespace Agent.Plugins.TestResultParser.Parser
{

    public class MochaTestResultParser : AbstractTestResultParser
    {
        // TODO: Need a hook for end of logs.
        // Needed for multiple reasons. Scenarios where i am expecting things and have not yet published the run
        // Needed where I have encountered test results but got no summary
        // It is true that it can be inferred due to the absence of the summary event, but I would like there to
        // be one telemetry event per parser run

        // TODO: Decide on a reset if no match found withing x lines logic after a previous match.
        // This can be fine tuned depending on the previous match
        // Infra already in place for this

        private TestRun _testRun;
        private MochaTestResultParserStateContext _stateContext;
        private int _currentTestRunId = 1;
        private MochaTestResultParserState _state;

        public sealed override string Name => nameof(MochaTestResultParser);
        public sealed override string Version => "1.0";

        /// <summary>
        /// Default constructor accepting only test run manager instance, rest of the requirements assume default values
        /// </summary>
        /// <param name="testRunManager"></param>
        public MochaTestResultParser(ITestRunManager testRunManager) : base(testRunManager)
        {
            Logger.Info("MochaTestResultParser.MochaTestResultParser : Starting mocha test result parser.");
            Telemetry.AddToCumulativeTelemetry(TelemetryConstants.EventArea, TelemetryConstants.Initialize, true);

            // Initialize the starting state of the parser
            _testRun = new TestRun($"{Name}/{Version}", _currentTestRunId) { TestRunSummary = new TestRunSummary() };
            _stateContext = new MochaTestResultParserStateContext();
            _state = MochaTestResultParserState.ExpectingTestResults;
        }

        /// <inheritdoc/>
        public override void Parse(LogData testResultsLine)
        {
            // State model for the mocha parser that defines the regexes to match against in each state
            // Each state re-orders the regexes based on the frequency of expected matches
            switch (_state)
            {
                // This state primarily looks for test results 
                // and transitions to the next one after a line of summary is encountered
                case MochaTestResultParserState.ExpectingTestResults:

                    if (MatchPassedTestCase(testResultsLine))
                    {
                        return;
                    }
                    if (MatchFailedTestCase(testResultsLine))
                    {
                        return;
                    }
                    if (MatchPendingTestCase(testResultsLine))
                    {
                        return;
                    }
                    if (MatchPassedSummary(testResultsLine))
                    {
                        return;
                    }

                    break;

                // This state primarily looks for test run summary 
                // If failed tests were found to be present transitions to the next one to look for stack traces
                // else goes back to the first state after publishing the run
                case MochaTestResultParserState.ExpectingTestRunSummary:

                    if (MatchPendingSummary(testResultsLine))
                    {
                        return;
                    }
                    if (MatchFailedSummary(testResultsLine))
                    {
                        return;
                    }
                    if (MatchPassedTestCase(testResultsLine))
                    {
                        return;
                    }
                    if (MatchFailedTestCase(testResultsLine))
                    {
                        return;
                    }
                    if (MatchPendingTestCase(testResultsLine))
                    {
                        return;
                    }
                    if (MatchPassedSummary(testResultsLine))
                    {
                        return;
                    }

                    break;

                // This state primarily looks for stack traces
                // If any other match occurs before all the expected stack traces are found it 
                // fires telemetry for unexpected behavior but moves on to the next test run
                case MochaTestResultParserState.ExpectingStackTraces:

                    if (MatchFailedTestCase(testResultsLine))
                    {
                        return;
                    }
                    if (MatchPassedTestCase(testResultsLine))
                    {
                        return;
                    }
                    if (MatchPendingTestCase(testResultsLine))
                    {
                        return;
                    }
                    if (MatchPassedSummary(testResultsLine))
                    {
                        return;
                    }

                    break;
            }

            // This is a mechanism to enforce matches that have to occur within 
            // a specific number of lines after encountering the previous match
            // one obvious usage is for successive summary lines containing passed,
            // pending and failed test summary
            if (_stateContext.LinesWithinWhichMatchIsExpected == 1)
            {
                AttemptPublishAndResetParser($"was expecting {_stateContext.ExpectedMatch} before line {testResultsLine.LineNumber} but no matches occurred");
                return;
            }

            if (_stateContext.LinesWithinWhichMatchIsExpected > 1)
            {
                _stateContext.LinesWithinWhichMatchIsExpected--;
            }
        }

        /// <summary>
        /// Publishes the run and resets the parser by resetting the state context and current state
        /// </summary>
        private void AttemptPublishAndResetParser(string reason = null)
        {
            if (!string.IsNullOrEmpty(reason))
            {
                Logger.Info($"MochaTestResultParser : Resetting the parser and attempting to publishing the test run : {reason}.");
                Telemetry.AddToCumulativeTelemetry(TelemetryConstants.EventArea,
                    TelemetryConstants.AttemptPublishAndResetParser, new List<string> { reason }, true);
            }

            PublishTestRun();

            ResetParser();
        }

        private void PublishTestRun()
        {
            // We have encountered failed test cases but no failed summary was encountered
            if (_testRun.FailedTests.Count != 0 && _testRun.TestRunSummary.TotalFailed == 0)
            {
                Logger.Error("MochaTestResultParser : Failed tests were encountered but no failed summary was encountered.");
                Telemetry.AddToCumulativeTelemetry(TelemetryConstants.EventArea,
                    TelemetryConstants.FailedTestCasesFoundButNoFailedSummary, new List<int> { _currentTestRunId }, true);
            }

            // We have encountered pending test cases but no pending summary was encountered
            if (_testRun.SkippedTests.Count != 0 && _testRun.TestRunSummary.TotalSkipped == 0)
            {
                Logger.Error("MochaTestResultParser : Skipped tests were encountered but no skipped summary was encountered.");
                Telemetry.AddToCumulativeTelemetry(TelemetryConstants.EventArea,
                    TelemetryConstants.PendingTestCasesFoundButNoFailedSummary, new List<int> { _currentTestRunId }, true);
            }

            // Ensure some summary data was detected before attempting a publish, ie. check if the state is not test results state
            switch (_state)
            {
                case MochaTestResultParserState.ExpectingTestResults:
                    if (_testRun.PassedTests.Count != 0)
                    {
                        Logger.Error("MochaTestResultParser : Passed tests were encountered but no passed summary was encountered.");
                        Telemetry.AddToCumulativeTelemetry(TelemetryConstants.EventArea,
                            TelemetryConstants.PassedTestCasesFoundButNoPassedSummary, new List<int> { _currentTestRunId }, true);
                    }
                    break;

                default:
                    // Publish the test run if reset and publish was called from any state other than the test results state
                    TestRunManager.Publish(_testRun);
                    _currentTestRunId++;
                    break;
            }
        }

        private void ResetParser()
        {
            // Refresh the context
            _stateContext = new MochaTestResultParserStateContext();

            // Start a new TestRun
            _testRun = new TestRun($"{Name}/{Version}", _currentTestRunId) { TestRunSummary = new TestRunSummary() };
            _state = MochaTestResultParserState.ExpectingTestResults;

            Logger.Info("MochaTestResultParser : Successfully reset the parser.");
        }

        /// <summary>
        /// Matches a line of input with the passed test case regex and performs appropriate actions 
        /// on a successful match
        /// </summary>
        /// <param name="testResultsLine"></param>
        /// <returns></returns>
        private bool MatchPassedTestCase(LogData testResultsLine)
        {
            var match = MochaTestResultParserRegexes.PassedTestCase.Match(testResultsLine.Message);

            if (!match.Success)
            {
                return false;
            }

            var testResult = new TestResult.TestResult();

            testResult.Outcome = TestOutcome.Passed;
            testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

            // Also since this is an action performed in context of a state should there be a separate function?
            // Should this intelligence come from the caller?

            switch (_state)
            {
                // If a passed test case is encountered while in the summary state it indicates either completion
                // or corruption of summary. Since Summary is Gospel to us, we will ignore the latter and publish
                // the run regardless. 
                case MochaTestResultParserState.ExpectingTestRunSummary:

                    AttemptPublishAndResetParser();
                    break;

                // If a passed test case is encountered while in the stack traces state it indicates corruption
                // or incomplete stack trace data
                case MochaTestResultParserState.ExpectingStackTraces:

                    // This check is safety check for when we try to parse stack trace contents
                    if (_stateContext.StackTracesToSkipParsingPostSummary != 0)
                    {
                        Logger.Error($"MochaTestResultParser : Expecting stack traces but found passed test case instead at line {testResultsLine.LineNumber}.");
                        Telemetry.AddToCumulativeTelemetry(TelemetryConstants.EventArea, TelemetryConstants.ExpectingStackTracesButFoundPassedTest,
                            new List<int> { _currentTestRunId }, true);
                    }

                    AttemptPublishAndResetParser();
                    break;
            }

            _testRun.PassedTests.Add(testResult);
            return true;
        }

        private bool MatchFailedTestCase(LogData testResultsLine)
        {
            var match = MochaTestResultParserRegexes.FailedTestCase.Match(testResultsLine.Message);

            if (!match.Success)
            {
                return false;
            }

            var testResult = new TestResult.TestResult();

            // Handling parse errors is unnecessary
            int.TryParse(match.Groups[RegexCaptureGroups.FailedTestCaseNumber].Value, out int testCaseNumber);

            // In the event the failed test case number does not match the expected test case number log an error and move on
            if (testCaseNumber != _stateContext.LastFailedTestCaseNumber + 1)
            {
                Logger.Error($"MochaTestResultParser : Expecting failed test case or stack trace with" +
                    $" number {_stateContext.LastFailedTestCaseNumber + 1} but found {testCaseNumber} instead");
                Telemetry.AddToCumulativeTelemetry(TelemetryConstants.EventArea, TelemetryConstants.UnexpectedFailedTestCaseNumber,
                    new List<int> { _currentTestRunId }, true);

                // If it was not 1 there's a good chance we read some random line as a failed test case hence consider it a
                // no match since the number did not match what we were expecting anyway
                if (testCaseNumber != 1)
                {
                    return false;
                }

                // If the number was 1 then there's a good chance this is the beginning of the next test run, hence reset and start over
                AttemptPublishAndResetParser($"was expecting failed test case or stack trace with number {_stateContext.LastFailedTestCaseNumber} but found" +
                    $" {testCaseNumber} instead");
            }

            // Increment either ways whether it was expected or context was reset and the encountered number was 1
            _stateContext.LastFailedTestCaseNumber++;

            // As of now we are ignoring stack traces
            if (_stateContext.StackTracesToSkipParsingPostSummary > 0)
            {
                _stateContext.StackTracesToSkipParsingPostSummary--;
                if (_stateContext.StackTracesToSkipParsingPostSummary == 0)
                {
                    // we can also choose to ignore extra failures post summary if the number is not 1
                    AttemptPublishAndResetParser();
                }

                return true;
            }

            // Also since this is an action performed in context of a state should there be a separate function?
            // Should this intelligence come from the caller?

            // If a failed test case is encountered while in the summary state it indicates either completion
            // or corruption of summary. Since Summary is Gospel to us, we will ignore the latter and publish
            // the run regardless. 
            if (_state == MochaTestResultParserState.ExpectingTestRunSummary)
            {
                AttemptPublishAndResetParser();
            }

            testResult.Outcome = TestOutcome.Failed;
            testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

            _testRun.FailedTests.Add(testResult);

            return true;
        }

        private bool MatchPendingTestCase(LogData testResultsLine)
        {
            var match = MochaTestResultParserRegexes.PendingTestCase.Match(testResultsLine.Message);

            if (!match.Success)
            {
                return false;
            }

            var testResult = new TestResult.TestResult();

            testResult.Outcome = TestOutcome.Skipped;
            testResult.Name = match.Groups[RegexCaptureGroups.TestCaseName].Value;

            // Also since this is an action performed in context of a state should there be a separate function?
            // Should this intelligence come from the caller?

            switch (_state)
            {
                // If a pending test case is encountered while in the summary state it indicates either completion
                // or corruption of summary. Since Summary is Gospel to us, we will ignore the latter and publish
                // the run regardless. 
                case MochaTestResultParserState.ExpectingTestRunSummary:

                    AttemptPublishAndResetParser();
                    break;

                // If a pending test case is encountered while in the stack traces state it indicates corruption
                // or incomplete stack trace data
                case MochaTestResultParserState.ExpectingStackTraces:

                    // This check is safety check for when we try to parse stack trace contents
                    if (_stateContext.StackTracesToSkipParsingPostSummary != 0)
                    {
                        Logger.Error("MochaTestResultParser : Expecting stack traces but found pending test case instead.");
                        Telemetry.AddToCumulativeTelemetry(TelemetryConstants.EventArea, TelemetryConstants.ExpectingStackTracesButFoundPendingTest,
                            new List<int> { _currentTestRunId }, true);
                    }

                    AttemptPublishAndResetParser();
                    break;
            }

            _testRun.SkippedTests.Add(testResult);
            return true;
        }

        private bool MatchPassedSummary(LogData testResultsLine)
        {
            var match = MochaTestResultParserRegexes.PassedTestsSummary.Match(testResultsLine.Message);

            if (!match.Success)
            {
                return false;
            }

            Logger.Info($"MochaTestResultParser : Passed test summary encountered at line {testResultsLine.LineNumber}.");

            // Unexpected matches for Passed summary
            // We expect summary ideally only when we are in the first state.
            switch (_state)
            {
                case MochaTestResultParserState.ExpectingTestRunSummary:

                    Logger.Error($"MochaTestResultParser : Was expecting atleast one test case before encountering" +
                        $" summary again at line {testResultsLine.LineNumber}");
                    Telemetry.AddToCumulativeTelemetry(TelemetryConstants.EventArea, TelemetryConstants.SummaryWithNoTestCases,
                        new List<int> { _currentTestRunId }, true);

                    AttemptPublishAndResetParser();
                    break;

                case MochaTestResultParserState.ExpectingStackTraces:

                    // If we were expecting more stack traces but got summary instead
                    if (_stateContext.StackTracesToSkipParsingPostSummary != 0)
                    {
                        Logger.Error("MochaTestResultParser : Expecting stack traces but found passed summary instead.");
                        Telemetry.AddToCumulativeTelemetry(TelemetryConstants.EventArea, TelemetryConstants.SummaryWithNoTestCases,
                            new List<int> { _currentTestRunId }, true);
                    }

                    AttemptPublishAndResetParser();
                    break;
            }

            _stateContext.LinesWithinWhichMatchIsExpected = 1;
            _stateContext.ExpectedMatch = "failed/pending tests summary";
            _state = MochaTestResultParserState.ExpectingTestRunSummary;
            _stateContext.LastFailedTestCaseNumber = 0;

            Logger.Info("MochaTestResultParser : Transitioned to state ExpectingTestRunSummary.");

            // Handling parse errors is unnecessary
            int.TryParse(match.Groups[RegexCaptureGroups.PassedTests].Value, out int totalPassed);

            _testRun.TestRunSummary.TotalPassed = totalPassed;

            // Fire telemetry if summary does not agree with parsed tests count
            if (_testRun.TestRunSummary.TotalPassed != _testRun.PassedTests.Count)
            {
                Logger.Error($"MochaTestResultParser : Passed tests count does not match passed summary" +
                    $" at line {testResultsLine.LineNumber}");
                Telemetry.AddToCumulativeTelemetry(TelemetryConstants.EventArea,
                    TelemetryConstants.PassedSummaryMismatch, new List<int> { _currentTestRunId }, true);
            }

            // Handling parse errors is unnecessary
            long.TryParse(match.Groups[RegexCaptureGroups.TestRunTime].Value, out long timeTaken);

            // Store time taken based on the unit used
            switch (match.Groups[RegexCaptureGroups.TestRunTimeUnit].Value)
            {
                case "ms":
                    _testRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken);
                    break;

                case "s":
                    _testRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 1000);
                    break;

                case "m":
                    _testRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 60 * 1000);
                    break;

                case "h":
                    _testRun.TestRunSummary.TotalExecutionTime = TimeSpan.FromMilliseconds(timeTaken * 60 * 60 * 1000);
                    break;
            }

            return true;
        }

        private bool MatchFailedSummary(LogData testResultsLine)
        {
            var match = MochaTestResultParserRegexes.FailedTestsSummary.Match(testResultsLine.Message);

            if (!match.Success)
            {
                return false;
            }

            Logger.Info($"MochaTestResultParser : Failed tests summary encountered at line {testResultsLine.LineNumber}.");

            _stateContext.LinesWithinWhichMatchIsExpected = 0;

            // Handling parse errors is unnecessary
            int.TryParse(match.Groups[RegexCaptureGroups.FailedTests].Value, out int totalFailed);

            _testRun.TestRunSummary.TotalFailed = totalFailed;
            _stateContext.StackTracesToSkipParsingPostSummary = totalFailed;


            Logger.Info("MochaTestResultParser : Transitioned to state ExpectingStackTraces.");
            _state = MochaTestResultParserState.ExpectingStackTraces;

            // If encountered failed tests does not match summary fire telemtry
            if (_testRun.TestRunSummary.TotalFailed != _testRun.FailedTests.Count)
            {
                Logger.Error($"MochaTestResultParser : Failed tests count does not match failed summary" +
                    $" at line {testResultsLine.LineNumber}");
                Telemetry.AddToCumulativeTelemetry(TelemetryConstants.EventArea,
                    TelemetryConstants.PassedSummaryMismatch, new List<int> { _currentTestRunId }, true);
            }

            return true;
        }

        private bool MatchPendingSummary(LogData testResultsLine)
        {
            var match = MochaTestResultParserRegexes.PendingTestsSummary.Match(testResultsLine.Message);

            if (!match.Success)
            {
                return false;
            }

            Logger.Info($"MochaTestResultParser : Pending tests summary encountered at line {testResultsLine.LineNumber}.");

            _stateContext.LinesWithinWhichMatchIsExpected = 1;

            // Handling parse errors is unnecessary
            int.TryParse(match.Groups[RegexCaptureGroups.PendingTests].Value, out int totalPending);

            _testRun.TestRunSummary.TotalSkipped = totalPending;

            // If encountered skipped tests does not match summary fire telemtry
            if (_testRun.TestRunSummary.TotalSkipped != _testRun.SkippedTests.Count)
            {
                Logger.Error($"MochaTestResultParser : Pending tests count does not match pending summary" +
                    $" at line {testResultsLine.LineNumber}");
                Telemetry.AddToCumulativeTelemetry(TelemetryConstants.EventArea,
                    TelemetryConstants.PendingSummaryMismatch, new List<int> { _currentTestRunId }, true);
            }

            return true;
        }
    }
}