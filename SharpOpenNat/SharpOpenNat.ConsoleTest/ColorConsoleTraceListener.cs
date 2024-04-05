//
// Authors:
//   Lucas Ontivero <lucasontivero@gmail.com>
//
// Copyright (C) 2014 Lucas Ontivero
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System.Diagnostics;

namespace SharpOpenNat.ConsoleTest;

public class ColorConsoleTraceListener : TraceListener
{
    private static readonly object _sync = new();

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? message)
    {
        TraceEvent(eventCache, source, eventType, id, message, Array.Empty<object>());
    }

    public override void TraceEvent(TraceEventCache? eventCache, string source, TraceEventType eventType, int id, string? format, params object?[]? args)
    {
        lock (_sync)
        {
            if (Filter != null && !Filter.ShouldTrace(eventCache, source, eventType, id, format, args, null, null)) return;
            var color = eventType switch
            {
                TraceEventType.Error => ConsoleColor.Red,
                TraceEventType.Warning => ConsoleColor.Yellow,
                TraceEventType.Information => ConsoleColor.Green,
                TraceEventType.Verbose => ConsoleColor.DarkCyan,
                _ => ConsoleColor.Gray,
            };
            var eventTypeString = Enum.GetName(typeof(TraceEventType), eventType);
            var message = source + " - " + eventTypeString + " > " + (args is not null && args.Length > 0 && format is not null ? string.Format(format, args) : format);

            WriteColor(message + Environment.NewLine, color);
        }
    }

    public override void Write(string? message)
    {
        WriteColor(message, ConsoleColor.Gray);
    }
    public override void WriteLine(string? message)
    {
        WriteColor(message + Environment.NewLine, ConsoleColor.Gray);
    }

    private static void WriteColor(string? message, ConsoleColor color)
    {
        Console.ForegroundColor = color;
        Console.Write(message);
        Console.ForegroundColor = ConsoleColor.Gray;
    }
}
