using System;
using System.Collections.Generic;
using System.Linq;
using Agent.Plugins.TestResultParser.TestRunManger;

namespace Agent.Plugins.TestResultParser.Parser
{
    public class ParserFactory
    {
        public static IEnumerable<AbstractTestResultParser> GetTestResultParsers(ITestRunManager testRunManager)
        {
            var interfaceType = typeof(AbstractTestResultParser);
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(x => x.GetTypes())
                .Where(x => interfaceType.IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
                .Select(x => (AbstractTestResultParser)Activator.CreateInstance(x, testRunManager));
        }
    }
}
