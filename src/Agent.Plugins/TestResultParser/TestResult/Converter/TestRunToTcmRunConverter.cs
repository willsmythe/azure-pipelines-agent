using System;
using Agent.Plugins.TestResultParser.TestResult;

namespace Agent.Plugins.TestResultParser.TestResult.Converter
{
    class TestRunToTcmRunConverter : ITestRunToTcmRunConverter
    {
        public void Convert(TestRun testRun, ITcmTestRun tcmTestRun)
        {
            if (testRun == null)
            {
                throw new ArgumentNullException(nameof(testRun));
            }

            throw new NotImplementedException();
        }
    }
}
