using System.Diagnostics;
using System.Net.Sockets;

namespace SharpOpenNat;

/// <summary>
/// 
/// </summary>
public static class NatDiscoverer
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
    public readonly static TraceSource TraceSource = new("SharpOpenNat");

    private static readonly Dictionary<string, NatDevice> Devices = new();

    // Finalizer is never used however its destructor, that releases the open ports, is invoked by the
    // process as part of the shuting down step. So, don't remove it!
#pragma warning disable IDE0052
    private static readonly Finalizer Finalizer = new();
#pragma warning restore IDE0052

    internal static readonly Timer RenewTimer = new(RenewMappings, null, 5000, 2000);

    /// <summary>
    /// Discovers and returns an UPnp or Pmp NAT device; otherwise a <see cref="NatDeviceNotFoundException">NatDeviceNotFoundException</see>
    /// exception is thrown after 3 seconds. 
    /// </summary>
    /// <returns>A NAT device</returns>
    /// <exception cref="NatDeviceNotFoundException">when no NAT found before 3 seconds.</exception>
    public static async Task<NatDevice> DiscoverDeviceAsync()
    {
        var cts = new CancellationTokenSource(3 * 1000);
        return await DiscoverDeviceAsync(PortMapper.Pmp | PortMapper.Upnp, cts);
    }

    /// <summary>
    /// Discovers and returns a NAT device for the specified type; otherwise a <see cref="NatDeviceNotFoundException">NatDeviceNotFoundException</see> 
    /// exception is thrown when it is cancelled. 
    /// </summary>
    /// <remarks>
    /// It allows to specify the NAT type to discover as well as the cancellation token in order.
    /// </remarks>
    /// <param name="portMapper">Port mapper protocol; Upnp, Pmp or both</param>
    /// <param name="cancellationTokenSource">Cancellation token source for cancelling the discovery process</param>
    /// <returns>A NAT device</returns>
    /// <exception cref="NatDeviceNotFoundException">when no NAT found before cancellation</exception>
    public static async Task<NatDevice> DiscoverDeviceAsync(PortMapper portMapper, CancellationTokenSource cancellationTokenSource)
    {
        Guard.IsTrue(portMapper.HasFlag(PortMapper.Upnp) || portMapper.HasFlag(PortMapper.Pmp), nameof(portMapper));
        Guard.IsNotNull(cancellationTokenSource, nameof(cancellationTokenSource));

        var devices = await DiscoverAsync(portMapper, false, cancellationTokenSource);

        NatDevice? device = null;

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
            TraceSource.LogInfo("Device not found. Common reasons:");
            TraceSource.LogInfo("\t* No device is present or,");
            TraceSource.LogInfo("\t* Upnp is disabled in the router or");
            TraceSource.LogInfo("\t* Antivirus software is filtering SSDP (discovery protocol).");
            throw new NatDeviceNotFoundException();
        }
        return device;
    }

    /// <summary>
    /// Discovers and returns all NAT devices for the specified type. If no NAT device is found it returns an empty enumerable
    /// </summary>
    /// <param name="portMapper">Port mapper protocol; Upnp, Pmp or both</param>
    /// <param name="cancellationTokenSource">Cancellation token source for cancelling the discovery process</param>
    /// <returns>All found NAT devices</returns>
    public static async Task<IEnumerable<NatDevice>> DiscoverDevicesAsync(PortMapper portMapper, CancellationTokenSource cancellationTokenSource)
    {
        Guard.IsTrue(portMapper.HasFlag(PortMapper.Upnp) || portMapper.HasFlag(PortMapper.Pmp), nameof(portMapper));
        Guard.IsNotNull(cancellationTokenSource, nameof(cancellationTokenSource));

        var devices = await DiscoverAsync(portMapper, false, cancellationTokenSource);
        return devices.ToArray();
    }

    private static async Task<IEnumerable<NatDevice>> DiscoverAsync(PortMapper portMapper, bool onlyOne, CancellationTokenSource cts)
    {
        TraceSource.LogInfo("Start Discovery");
        var searcherTasks = new List<Task<IEnumerable<NatDevice>>>();
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
        TraceSource.LogInfo("Stop Discovery");

        var devices = searcherTasks.SelectMany(x => x.Result);
        foreach (var device in devices)
        {
            var key = device.ToString()!;
            if (Devices.TryGetValue(key, out NatDevice? nat))
            {
                nat.Touch();
            }
            else
            {
                Devices.Add(key, device);
            }
        }
        return devices;
    }

    /// <summary>
    /// Release all ports opened by SharpOpenNat. 
    /// </summary>
    public static void ReleaseAll()
    {
        foreach (var device in Devices.Values)
        {
            device.ReleaseAll();
        }
    }

    internal static void ReleaseSessionMappings()
    {
        foreach (var device in Devices.Values)
        {
            device.ReleaseSessionMappings();
        }
    }

    private static void RenewMappings(object? state)
    {
        Task.Factory.StartNew(async () =>
        {
            foreach (var device in Devices.Values)
            {
                await device.RenewMappings();
            }
        });
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="startingPort"></param>
    /// <returns></returns>
    public static async Task<int> GetAvailablePort(int startingPort)
    {
        var portArray = await GetUsedPorts();

        for (int i = startingPort; i < ushort.MaxValue; i++)
        {
            if (!portArray.Contains(i))
            {
                return i;
            }
        }

        return 0;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="portArray"></param>
    /// <param name="startingPort"></param>
    /// <returns></returns>
    public static int GetAvailablePort(IList<int> portArray, int startingPort)
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

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public static async Task<List<int>> GetUsedPorts()
    {
        var cts = new CancellationTokenSource(3 * 1000);
        var device = await DiscoverDeviceAsync(PortMapper.Upnp | PortMapper.Pmp, cts);

        var portArray = new List<int>();

        foreach (var mapping in await device.GetAllMappingsAsync())
        {
            portArray.Add(mapping.PrivatePort);
            portArray.Add(mapping.PublicPort);
        }

        portArray.Sort();

        return portArray;
    }
}
