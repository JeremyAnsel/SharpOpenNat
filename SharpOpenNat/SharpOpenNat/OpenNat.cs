//
// Authors:
//   Alan McGovern alan.mcgovern@gmail.com
//   Ben Motmans <ben.motmans@gmail.com>
//   Lucas Ontivero lucasontivero@gmail.com
//   Luigi Trabacchin <trabacchin.luigi@gmail.com>
//
// Copyright (C) 2006 Alan McGovern
// Copyright (C) 2007 Ben Motmans
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

namespace SharpOpenNat;

/// <summary>
/// 
/// </summary>
public static class OpenNat
{
    /// <summary>
    /// The <see href="http://msdn.microsoft.com/en-us/library/vstudio/system.diagnostics.tracesource">TraceSource</see> instance
    /// used for debugging and <see href="https://github.com/lontivero/Open.Nat/wiki/Troubleshooting">Troubleshooting</see>.
    /// </summary>
    /// <example>
    /// NatUtility.TraceSource.Switch.Level = SourceLevels.Verbose;
    /// NatUtility.TraceSource.Listeners.Add(new ConsoleListener());
    /// </example>
    /// <remarks>
    /// At least one trace listener has to be added to the Listeners collection if a trace is required; if no listener is added
    /// there will no be tracing to analyse.
    /// </remarks>
    /// <remarks>
    /// SharpOpenNat only supports SourceLevels.Verbose, SourceLevels.Error, SourceLevels.Warning and SourceLevels.Information.
    /// </remarks>
    public static readonly TraceSource TraceSource = new("SharpOpenNat");

    private static INatDiscoverer? _natDiscoverer;
    /// <summary>
    /// Lazy loaded singleton implementation of INatDiscoverer
    /// </summary>
    public static INatDiscoverer Discoverer
    {
        get
        {
            _natDiscoverer ??= new NatDiscoverer();
            return _natDiscoverer;
        }
    }

    // Finalizer is never used however its destructor, that releases the open ports, is invoked by the
    // process as part of the shuting down step. So, don't remove it!

    [System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0052:Remove unread private members", Justification = "The finalizer is needed to dsipose nat discoverer ")]
    private static readonly OpenNatFinalizer Finalizer = new();


    private class OpenNatFinalizer
    {
        ~OpenNatFinalizer()
        {
            _natDiscoverer?.Dispose();
        }
    }
}
