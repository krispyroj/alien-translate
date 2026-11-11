using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Text;

namespace AlienTranslate
{
    internal static class ScreenText
    {
        private static readonly Vector[] HitOffsets =
        {
            new Vector(0, 0), new Vector(-2, -2), new Vector(2, -2),
            new Vector(-2, 2), new Vector(2, 2)
        };

        // UI Automation calls can wait on another process. Run these on a worker thread.
        internal static string ReadSelection(System.Drawing.Point location, IntPtr window, int processId)
        {
            // A native context menu can cover the pointer before the worker runs.
            string text = ReadSafely(() => SelectionFrom(AutomationElement.FromHandle(window), processId));
            if (text != null)
                return text;

            var point = new Point(location.X, location.Y);
            foreach (var offset in HitOffsets)
            {
                text = ReadSafely(() => SelectionFrom(AutomationElement.FromPoint(point + offset), processId));
                if (text != null)
                    return text;
            }
            return null;
        }

        private static string SelectionFrom(AutomationElement element, int processId)
        {
            for (int depth = 0; depth < 12 && element != null; depth++)
            {
                var properties = element.Current;
                if (properties.IsPassword || properties.ProcessId != processId)
                    return null;

                object pattern;
                if (element.TryGetCurrentPattern(TextPattern.Pattern, out pattern))
                {
                    var ranges = ((TextPattern)pattern).GetSelection();
                    if (ranges == null || ranges.Length == 0)
                    {
                        element = TreeWalker.RawViewWalker.GetParent(element);
                        continue;
                    }
                    var parts = new List<string>();
                    foreach (var range in ranges)
                    {
                        string selected = range.GetText(-1);
                        if (!string.IsNullOrWhiteSpace(selected))
                            parts.Add(selected);
                    }
                    if (parts.Count > 0)
                        return string.Join(Environment.NewLine, parts);
                }

                element = TreeWalker.RawViewWalker.GetParent(element);
            }
            return null;
        }

        internal static string ReadAt(System.Drawing.Point location, int ownProcessId)
        {
            var point = new Point(location.X, location.Y);
            return ReadSafely(() =>
            {
                var element = AutomationElement.FromPoint(point);
                for (int depth = 0; depth < 8 && element != null; depth++)
                {
                    var properties = element.Current;
                    if (properties.IsPassword || properties.ProcessId == ownProcessId)
                        return null;

                    object pattern;
                    if (element.TryGetCurrentPattern(TextPattern.Pattern, out pattern))
                    {
                        var range = ((TextPattern)pattern).RangeFromPoint(point);
                        if (range != null)
                        {
                            range.ExpandToEnclosingUnit(TextUnit.Line);
                            foreach (var bounds in range.GetBoundingRectangles())
                            {
                                if (bounds.Contains(point))
                                    return range.GetText(-1).TrimEnd('\r', '\n');
                            }
                        }
                    }

                    if (properties.ControlType == ControlType.Text)
                        return properties.Name;

                    element = TreeWalker.RawViewWalker.GetParent(element);
                }
                return null;
            });
        }

        private static string ReadSafely(Func<string> read)
        {
            try { return read(); }
            catch (ElementNotAvailableException) { }
            catch (COMException) { }
            catch (InvalidOperationException) { }
            catch (UnauthorizedAccessException) { }
            catch (ArgumentException) { }
            return null;
        }
    }
}
