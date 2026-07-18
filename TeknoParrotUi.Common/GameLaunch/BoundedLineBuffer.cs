using System;
using System.Collections.Generic;
using System.Text;

namespace TeknoParrotUi.Common.GameLaunch
{
    /// <summary>
    /// Small bounded buffer for live UI consoles. It keeps the newest output,
    /// caps pathological single lines, and makes truncation visible instead of
    /// allowing a noisy child process to grow the UI string without limit.
    /// </summary>
    internal sealed class BoundedLineBuffer
    {
        private const string LineTruncatedMarker = " … [line truncated]";
        private const string HistoryTruncatedNotice =
            "(earlier launch output was removed to limit memory use)";

        private readonly object _sync = new object();
        private readonly Queue<string> _lines = new Queue<string>();
        private readonly int _maxLines;
        private readonly int _maxCharacters;
        private readonly int _maxLineCharacters;
        private int _characterCount;
        private bool _historyTruncated;

        internal BoundedLineBuffer(
            int maxLines,
            int maxCharacters,
            int maxLineCharacters)
        {
            if (maxLines <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxLines));
            if (maxCharacters <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxCharacters));
            if (maxLineCharacters <= 0 || maxLineCharacters > maxCharacters)
                throw new ArgumentOutOfRangeException(nameof(maxLineCharacters));

            _maxLines = maxLines;
            _maxCharacters = maxCharacters;
            _maxLineCharacters = maxLineCharacters;
        }

        internal int Count
        {
            get { lock (_sync) return _lines.Count; }
        }

        internal int CharacterCount
        {
            get { lock (_sync) return _characterCount; }
        }

        internal bool WasTruncated
        {
            get { lock (_sync) return _historyTruncated; }
        }

        internal void Clear()
        {
            lock (_sync)
            {
                _lines.Clear();
                _characterCount = 0;
                _historyTruncated = false;
            }
        }

        internal void AppendLine(string line)
        {
            line ??= string.Empty;
            lock (_sync)
            {
                if (line.Length > _maxLineCharacters)
                {
                    var payloadLength = Math.Max(
                        0,
                        _maxLineCharacters - LineTruncatedMarker.Length);
                    line = line.Substring(0, payloadLength) +
                           LineTruncatedMarker.Substring(
                               0,
                               Math.Min(
                                   LineTruncatedMarker.Length,
                                   _maxLineCharacters - payloadLength));
                    _historyTruncated = true;
                }

                var addedCharacters = line.Length + Environment.NewLine.Length;
                while (_lines.Count > 0 &&
                       (_lines.Count >= _maxLines ||
                        _characterCount + addedCharacters > _maxCharacters))
                {
                    var removed = _lines.Dequeue();
                    _characterCount -=
                        removed.Length + Environment.NewLine.Length;
                    _historyTruncated = true;
                }

                _lines.Enqueue(line);
                _characterCount += addedCharacters;
            }
        }

        internal string GetText()
        {
            lock (_sync)
            {
                var extra = _historyTruncated
                    ? HistoryTruncatedNotice.Length + Environment.NewLine.Length
                    : 0;
                var result = new StringBuilder(_characterCount + extra);
                if (_historyTruncated)
                    result.AppendLine(HistoryTruncatedNotice);
                foreach (var line in _lines)
                    result.AppendLine(line);
                return result.ToString();
            }
        }
    }
}
