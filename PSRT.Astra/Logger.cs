using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

        public enum Domain
        {
            Astra,
            ArksLayer
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
        
        public void Info(Domain domain, string message) => Write(Level.Info, domain, message);
        public void Warning(Domain domain, string message) => Write(Level.Warning, domain, message);
        public void Error(Domain domain, string message) => Write(Level.Error, domain, message);

        public void Write(Level level, Domain domain, string message)
        {
            var line = $"{_GetPrefix(level, domain)} {message}";

            lock (_Lock)
                _Builder.AppendLine(line);
        }

        public void Info(Domain domain, string message, Exception exception) => Write(Level.Info, domain, message, exception);
        public void Warning(Domain domain, string message, Exception exception) => Write(Level.Warning, domain, message, exception);
        public void Error(Domain domain, string message, Exception exception) => Write(Level.Error, domain, message, exception);

        public void Write(Level level, Domain domain, string message, Exception exception)
        {
            var line = $"{_GetPrefix(level, domain)} {message}";
            var exceptionString = exception.ToString();

            lock (_Lock)
            {
                _Builder.AppendLine(line);
                _Builder.AppendLine(exceptionString);
            }
        }

        private string _GetPrefix(Level level, Domain domain)
        {
            var levelString = _GetLevelString(level);
            return $"[{levelString} {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")}]({domain})";
        }

        private string _GetLevelString(Level level)
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
    }
}
