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

        public void Info(string message) => Write(Level.Info, message);
        public void Warning(string message) => Write(Level.Warning, message);
        public void Error(string message) => Write(Level.Error, message);

        public void Write(Level level, string message)
        {
            var line = $"{_GetPrefix(level)} {message}";

            lock (_Lock)
                _Builder.AppendLine(line);
        }

        public void Info(string message, Exception exception) => Write(Level.Info, message, exception);
        public void Warning(string message, Exception exception) => Write(Level.Warning, message, exception);
        public void Error(string message, Exception exception) => Write(Level.Error, message, exception);

        public void Write(Level level, string message, Exception exception)
        {
            var line = $"{_GetPrefix(level)} {message}";
            var exceptionString = exception.ToString();

            lock (_Lock)
            {
                _Builder.AppendLine(line);
                _Builder.AppendLine(exceptionString);
            }
        }

        private string _GetPrefix(Level level)
        {
            var levelString = _GetLevelString(level);
            return $"[{levelString} {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")}]";
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
