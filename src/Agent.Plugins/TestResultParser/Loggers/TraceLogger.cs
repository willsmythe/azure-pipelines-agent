namespace Agent.Plugins.TestResultParser.Loggers
{
    using System;

    public class TraceLogger : ITraceLogger
    {
        private TraceLogger()
        {
        }

        private static ITraceLogger _instance;

        /// <summary>
        /// Gets the singleton instance of diagnostics data collector.
        /// </summary>
        public static ITraceLogger Instance
        {
            get => _instance ?? (_instance = new TraceLogger());
            set => _instance = value;
        }

        #region interface implementation

        void ITraceLogger.Warning(string text)
        {
            Console.WriteLine(text);
        }

        void ITraceLogger.Error(string error)
        {
            Console.WriteLine(error);
        }

        void ITraceLogger.Verbose(string text)
        {
            Console.WriteLine(text);
        }

        void ITraceLogger.Info(string text)
        {
            Console.WriteLine(text);
        }

        #endregion
        
        /// <inheritdoc />
        public static void Error(string error)
        {
            Instance.Error(error);
        }
        /// <inheritdoc />
        public static void Info(string text)
        {
            Instance.Info(text);
        }
        /// <inheritdoc />
        public static void Verbose(string text)
        {
            Instance.Verbose(text);
        }

        /// <inheritdoc />
        public static void Warning(string text)
        {
            Instance.Warning(text);
        }
    }
}
