using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.Text;

namespace LMLocal.Infrastructure.Autocompletions.InlineCompletion
{
    /// <summary>
    /// Remembers lines the caret recently left (in any document) so FIM requests can carry one of
    /// them as extra context. A short history is kept so that ordinary typing (whose previous line
    /// is already inside the prefix) does not hide a more useful cross-file visit.
    /// </summary>
    internal static class RecentCaretContext
    {
        private const int MaxVisits = 8;
        private const int MaxLineLength = 200;
        private const int MinLineLength = 3;

        private static readonly object Gate = new object();
        private static readonly List<CaretVisit> Visits = new List<CaretVisit>();

        internal sealed class CaretVisit
        {
            internal string FilePath;
            internal int LineNumber; // 1-based
            internal string Text;
        }

        /// <summary>Records the line containing <paramref name="position"/>.</summary>
        internal static void Record(ITextSnapshot snapshot, int position)
        {
            if (snapshot == null)
                return;

            var line = snapshot.GetLineFromPosition(position);
            Record(SuggestionTagger.GetFilePath(snapshot), line.LineNumber + 1, line.GetText());
        }

        internal static void Record(string filePath, int lineNumber, string lineText)
        {
            string text = lineText?.Trim();
            if (string.IsNullOrEmpty(text))
                return;

            if (text.Length > MaxLineLength)
                text = text.Substring(0, MaxLineLength) + "…";

            lock (Gate)
            {
                if (Visits.Count > 0)
                {
                    var top = Visits[0];
                    if (string.Equals(top.FilePath, filePath, StringComparison.OrdinalIgnoreCase) &&
                        top.LineNumber == lineNumber &&
                        top.Text == text)
                        return;
                }

                Visits.Insert(0, new CaretVisit
                {
                    FilePath = filePath ?? string.Empty,
                    LineNumber = lineNumber,
                    Text = text
                });

                if (Visits.Count > MaxVisits)
                    Visits.RemoveAt(MaxVisits);
            }
        }

        internal static CaretVisit Last
        {
            get { lock (Gate) { return Visits.Count > 0 ? Visits[0] : null; } }
        }

        internal static void Clear()
        {
            lock (Gate) { Visits.Clear(); }
        }

        /// <summary>
        /// One-line comment (syntax chosen by file extension) with the most recent visited caret line
        /// that is not the current line and not already present in the prefix/suffix.
        /// Returns null when there is nothing useful to add.
        /// </summary>
        internal static string BuildAnnotation(string currentFilePath, int currentLineNumber, string prefix, string suffix)
        {
            CaretVisit[] visits;
            lock (Gate) { visits = Visits.ToArray(); }

            foreach (var visit in visits)
            {
                if (visit.Text.Length < MinLineLength)
                    continue;

                bool sameFile = string.Equals(visit.FilePath, currentFilePath, StringComparison.OrdinalIgnoreCase);
                if (sameFile && visit.LineNumber == currentLineNumber)
                    continue;

                if (Contains(prefix, visit.Text) || Contains(suffix, visit.Text))
                    continue;

                return Format(visit, sameFile, currentFilePath);
            }

            return null;
        }

        private static string Format(CaretVisit visit, bool sameFile, string currentFilePath)
        {
            string location = sameFile
                ? "line " + visit.LineNumber
                : GetDisplayName(visit.FilePath) + ":" + visit.LineNumber;

            string open, close;
            CommentTokens(currentFilePath, out open, out close);
            return open + "recently visited " + location + ": " + visit.Text + close;
        }

        private static bool Contains(string haystack, string needle)
        {
            return !string.IsNullOrEmpty(haystack) && haystack.IndexOf(needle, StringComparison.Ordinal) >= 0;
        }

        private static string GetDisplayName(string filePath)
        {
            try
            {
                string name = Path.GetFileName(filePath);
                return string.IsNullOrEmpty(name) ? "other file" : name;
            }
            catch (ArgumentException)
            {
                return "other file";
            }
        }

        private static void CommentTokens(string filePath, out string open, out string close)
        {
            string ext;
            try
            {
                ext = (Path.GetExtension(filePath) ?? string.Empty).ToLowerInvariant();
            }
            catch (ArgumentException)
            {
                ext = string.Empty;
            }

            switch (ext)
            {
                case ".py": case ".pyw": case ".rb": case ".sh": case ".bash": case ".zsh":
                case ".ps1": case ".yaml": case ".yml": case ".toml": case ".r": case ".pl":
                    open = "# "; close = string.Empty; break;
                case ".sql": case ".lua": case ".hs": case ".elm":
                    open = "-- "; close = string.Empty; break;
                case ".bat": case ".cmd":
                    open = "REM "; close = string.Empty; break;
                case ".xml": case ".xaml": case ".html": case ".htm": case ".svg":
                case ".csproj": case ".props": case ".targets":
                    open = "<!-- "; close = " -->"; break;
                case ".css": case ".scss": case ".less":
                    open = "/* "; close = " */"; break;
                default:
                    open = "// "; close = string.Empty; break;
            }
        }
    }
}
