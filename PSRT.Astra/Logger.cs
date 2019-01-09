using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;

namespace PSRT.Astra
{
    public class Logger
    {
        public enum Level
        {
            Info,
            Warning,
            Error
        }

        private object _Lock = new object();
        private StringBuilder _Builder = new StringBuilder();

        public string Content
        {
            get
            {
                lock (_Lock)
                    return _Builder.ToString();
            }
        }

        public void Info(string domain, string message) => Write(Level.Info, domain, message);
        public void Warning(string domain, string message) => Write(Level.Warning, domain, message);
        public void Error(string domain, string message) => Write(Level.Error, domain, message);

        public void Write(Level level, string domain, string message)
        {
            var line = $"{_GetPrefix(level, domain)} {message}";

            lock (_Lock)
                _Builder.AppendLine(line);
        }

        public void Info(string domain, string message, Exception exception) => Write(Level.Info, domain, message, exception);
        public void Warning(string domain, string message, Exception exception) => Write(Level.Warning, domain, message, exception);
        public void Error(string domain, string message, Exception exception) => Write(Level.Error, domain, message, exception);

        public void Write(Level level, string domain, string message, Exception exception)
        {
            var line = $"{_GetPrefix(level, domain)} {message}";
            var exceptionString = _GetExceptionStringInvariantCulture(exception);

            lock (_Lock)
            {
                _Builder.AppendLine(line);
                _Builder.AppendLine(exceptionString);
            }
        }

        private static string _GetPrefix(Level level, string domain)
        {
            var levelString = _GetLevelString(level);
            return $"[{levelString} {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")}]({domain})";
        }

        private static string _GetLevelString(Level level)
        {
            const int length = 5;

            switch (level)
            {
                case Level.Info:
                    return "INFO".PadRight(length);
                case Level.Warning:
                    return "WARN".PadRight(length);
                case Level.Error:
                    return "ERROR".PadRight(length);
            }

            return new string(' ', length);
        }

        private static string _GetExceptionStringInvariantCulture(Exception ex)
        {
            var currentCulture = Thread.CurrentThread.CurrentCulture;
            var currentUICulture = Thread.CurrentThread.CurrentUICulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

                return ex.ToString();
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = currentCulture;
                Thread.CurrentThread.CurrentUICulture = currentUICulture;
            }
        }
    }
}
