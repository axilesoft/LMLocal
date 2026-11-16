using System;
using System.IO;
using System.Text;

namespace LMLocal.Infrastructure.Autocompletions
{
    /// <summary>
    /// Appends FIM autocomplete requests/responses to R:\LM LOCAL.log for debugging.
    /// Never throws: debug logging must not break autocomplete.
    /// </summary>
    internal static class AutocompleteDebugLog
    {
        private const string LogPath = @"R:\LM LOCAL.log";
        private static readonly object Gate = new object();

        /// <summary>Disabled by unit tests to keep the log free of test traffic.</summary>
        internal static bool Enabled { get; set; } = true;

        internal static void Write(string modelId, string prompt, string response)
        {
            if (!Enabled)
                return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("===== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " | " + modelId + " =====");
                sb.AppendLine("----- prompt -----");
                sb.AppendLine(prompt ?? string.Empty);
                sb.AppendLine("----- response -----");
                sb.AppendLine(response ?? string.Empty);
                sb.AppendLine();

                lock (Gate)
                {
                    File.AppendAllText(LogPath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
