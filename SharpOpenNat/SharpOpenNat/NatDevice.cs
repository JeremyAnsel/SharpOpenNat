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

using System.Net;

namespace SharpOpenNat;


internal abstract class NatDevice : INatDevice
{

    public abstract IPEndPoint HostEndPoint { get; }


    public abstract IPAddress LocalAddress { get; }

    private readonly HashSet<Mapping> _openedMapping = new();

    protected DateTime LastSeen { get; private set; }

    internal void Touch()
    {
        LastSeen = DateTime.Now;
    }

    public abstract Task CreatePortMapAsync(Mapping mapping, CancellationToken cancellationToken = default);

    public abstract Task DeletePortMapAsync(Mapping mapping, CancellationToken cancellationToken = default);

    public abstract Task<Mapping[]> GetAllMappingsAsync(CancellationToken cancellationToken = default);

    public abstract Task<IPAddress?> GetExternalIPAsync(CancellationToken cancellationToken = default);

    public abstract Task<Mapping?> GetSpecificMappingAsync(Protocol protocol, int port, CancellationToken cancellationToken = default);

    /// <inheritdoc />
    protected void RegisterMapping(Mapping mapping)
    {
        _openedMapping.Remove(mapping);
        _openedMapping.Add(mapping);
    }

    /// <inheritdoc />
    protected void UnregisterMapping(Mapping mapping)
    {
        _openedMapping.RemoveWhere(x => x.Equals(mapping));
    }


    internal async Task ReleaseMappings(IEnumerable<Mapping> mappings, CancellationToken cancellationToken = default)
    {
        var maparr = mappings.ToArray();
        var mapCount = maparr.Length;
        OpenNat.TraceSource.LogInfo("{0} ports to close", mapCount);
        for (var i = 0; i < mapCount; i++)
        {
            var mapping = _openedMapping.ElementAt(i);

            try
            {
                await DeletePortMapAsync(mapping, cancellationToken);
                OpenNat.TraceSource.LogInfo(mapping + " port successfully closed");
            }
            catch (Exception)
            {
                OpenNat.TraceSource.LogError(mapping + " port couldn't be close");
            }
        }
    }

    internal async Task ReleaseAllAsync(CancellationToken cancellationToken = default)
    {
        await ReleaseMappings(_openedMapping, cancellationToken);
    }

    internal async Task  ReleaseSessionMappingsAsync(CancellationToken cancellationToken = default)
    {
        var mappings = from m in _openedMapping
                       where m.LifetimeType == MappingLifetime.Session
                       select m;

        await ReleaseMappings(mappings, cancellationToken);
    }

    /// <exception cref="OperationCanceledException" />
    /// <exception cref="ObjectDisposedException" />
    internal async Task RenewMappings(CancellationToken cancellationToken = default)
    {
        var mappings = _openedMapping.Where(x => x.ShoundRenew()).ToArray();
        foreach (var mapping in mappings)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var m = mapping;
            await RenewMapping(m, cancellationToken);
        }
    }

    private async Task RenewMapping(Mapping mapping, CancellationToken cancellationToken = default)
    {
        var renewMapping = new Mapping(mapping);
        try
        {
            renewMapping.Expiration = DateTime.UtcNow.AddSeconds(mapping.Lifetime);

            OpenNat.TraceSource.LogInfo("Renewing mapping {0}", renewMapping);
            await CreatePortMapAsync(renewMapping, cancellationToken);
            OpenNat.TraceSource.LogInfo("Next renew scheduled at: {0}",
                                              renewMapping.Expiration.ToLocalTime().TimeOfDay);
        }
        catch (Exception)
        {
            OpenNat.TraceSource.LogWarn("Renew {0} failed", mapping);
        }
    }
}
