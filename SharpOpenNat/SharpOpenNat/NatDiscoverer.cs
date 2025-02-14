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

using System.Net.Sockets;

namespace SharpOpenNat;

internal class NatDiscoverer : INatDiscoverer
{
    private readonly Dictionary<string, NatDevice> _devices = new();

    private readonly Timer _renewTimer;

    public NatDiscoverer()
    {
        _renewTimer = new(RenewMappings, null, 5000, 2000);
    }


    public async Task<INatDevice> DiscoverDeviceAsync(CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(3 * 1000);
        return await DiscoverDeviceAsync(PortMapper.Pmp | PortMapper.Upnp, cts.Token);
    }

    public async Task<INatDevice> DiscoverDeviceAsync(PortMapper portMapper, CancellationToken cancellationTokenSource = default)
    {
        Guard.IsTrue(portMapper.HasFlag(PortMapper.Upnp) || portMapper.HasFlag(PortMapper.Pmp), nameof(portMapper));
        Guard.IsNotNull(cancellationTokenSource, nameof(cancellationTokenSource));

        var devices = await DiscoverAsync(portMapper, false, cancellationTokenSource);

        INatDevice? device = null;

        foreach (var currentDevice in devices)
        {
            AddressFamily addressFamily = currentDevice.HostEndPoint.AddressFamily;

            if (addressFamily != AddressFamily.InterNetworkV6)
            {
                device = currentDevice;
                break;
            }
        }

        device ??= devices.FirstOrDefault();

        if (device is null)
        {
            OpenNat.TraceSource.LogInfo("Device not found. Common reasons:");
            OpenNat.TraceSource.LogInfo("\t* No device is present or,");
            OpenNat.TraceSource.LogInfo("\t* Upnp is disabled in the router or");
            OpenNat.TraceSource.LogInfo("\t* Antivirus software is filtering SSDP (discovery protocol).");
            throw new NatDeviceNotFoundException();
        }
        return device;
    }

    public async Task<IEnumerable<INatDevice>> DiscoverDevicesAsync(PortMapper portMapper, CancellationToken cancellationTokenSource = default)
    {
        Guard.IsTrue(portMapper.HasFlag(PortMapper.Upnp) || portMapper.HasFlag(PortMapper.Pmp), nameof(portMapper));
        Guard.IsNotNull(cancellationTokenSource, nameof(cancellationTokenSource));

        var devices = await DiscoverAsync(portMapper, false, cancellationTokenSource);
        return devices.ToArray();
    }

    private async Task<IEnumerable<INatDevice>> DiscoverAsync(PortMapper portMapper, bool onlyOne, CancellationToken cancellationToken)
    {
        OpenNat.TraceSource.LogInfo("Start Discovery");
        var searcherTasks = new List<Task<IEnumerable<NatDevice>>>();
        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            if (portMapper.HasFlag(PortMapper.Upnp))
            {
                var upnpSearcher = new UpnpSearcher(new IPAddressesProvider());
                upnpSearcher.DeviceFound += (sender, args) => { if (onlyOne) cts.Cancel(); };
                searcherTasks.Add(upnpSearcher.Search(cts.Token));
            }
            if (portMapper.HasFlag(PortMapper.Pmp))
            {
                var pmpSearcher = new PmpSearcher(new IPAddressesProvider());
                pmpSearcher.DeviceFound += (sender, args) => { if (onlyOne) cts.Cancel(); };
                searcherTasks.Add(pmpSearcher.Search(cts.Token));
            }

            await Task.WhenAll(searcherTasks);
        }
        OpenNat.TraceSource.LogInfo("Stop Discovery");

        var devices = searcherTasks.SelectMany(x => x.Result);
        foreach (var device in devices)
        {
            var key = device.ToString()!;
            if (_devices.TryGetValue(key, out NatDevice? nat))
            {
                nat.Touch();
            }
            else
            {
                _devices.Add(key, device);
            }
        }
        return devices;
    }

    /// <summary>
    /// Release all ports opened by SharpOpenNat. 
    /// </summary>
    public async Task ReleaseAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var device in _devices.Values)
        {
            await device.ReleaseAllAsync(cancellationToken);
        }
    }

    private async Task ReleaseSessionMappingsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var device in _devices.Values)
        {
            await device.ReleaseSessionMappingsAsync(cancellationToken);
        }
    }

    private async void RenewMappings(object? state)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var currentDevices = _devices.Values.ToArray();
            foreach (var device in currentDevices)
            {
                await device.RenewMappings(cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            OpenNat.TraceSource.LogInfo("Nat discoverer did not manage to renew all mapings before a new timer cycle");
        }
        catch (Exception ex)
        {
            OpenNat.TraceSource.LogError("Nat discoverer did not manage to renew all mapings because {0}", ex);
        }
    }

    

    /// <summary>
    /// 
    /// </summary>
    /// <param name="portArray"></param>
    /// <param name="startingPort"></param>
    /// <returns></returns>
    public int GetAvailablePort(IList<int> portArray, int startingPort)
    {
        for (int i = startingPort; i < ushort.MaxValue; i++)
        {
            if (!portArray.Contains(i))
            {
                return i;
            }
        }

        return 0;
    }

    

    public void Dispose()
    {
        OpenNat.TraceSource.LogInfo("Closing ports opened in this session");
        _renewTimer.Dispose();
        Task.Run(async () => await ReleaseSessionMappingsAsync()).Wait();
    }
}
