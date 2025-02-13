

namespace SharpOpenNat
{
    /// <summary>
    /// Discovers NAT device;
    /// </summary>
    public interface INatDiscoverer : IDisposable
    {
        /// <summary>
        /// Discovers and returns an UPnp or Pmp NAT device; otherwise a <see cref="NatDeviceNotFoundException">NatDeviceNotFoundException</see>
        /// exception is thrown after 3 seconds. 
        /// </summary>
        /// <returns>A NAT device</returns>
        /// <exception cref="NatDeviceNotFoundException">when no NAT found before 3 seconds.</exception>
        Task<INatDevice> DiscoverDeviceAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Discovers and returns a NAT device for the specified type; otherwise a <see cref="NatDeviceNotFoundException">NatDeviceNotFoundException</see> 
        /// exception is thrown when it is cancelled. 
        /// </summary>
        /// <remarks>
        /// It allows to specify the NAT type to discover as well as the cancellation token in order.
        /// </remarks>
        /// <param name="portMapper">Port mapper protocol; Upnp, Pmp or both</param>
        /// <param name="cancellationToken">Cancellation token for cancelling the discovery process</param>
        /// <returns>A NAT device</returns>
        /// <exception cref="NatDeviceNotFoundException">when no NAT found before cancellation</exception>
        Task<INatDevice> DiscoverDeviceAsync(PortMapper portMapper, CancellationToken cancellationToken = default);

        /// <summary>
        /// Discovers and returns all NAT devices for the specified type. If no NAT device is found it returns an empty enumerable
        /// </summary>
        /// <param name="portMapper">Port mapper protocol; Upnp, Pmp or both</param>
        /// <param name="cancellationToken">Cancellation token for cancelling the discovery process</param>
        /// <returns>All found NAT devices</returns>
        Task<IEnumerable<INatDevice>> DiscoverDevicesAsync(PortMapper portMapper, CancellationToken cancellationToken = default);
    }
}