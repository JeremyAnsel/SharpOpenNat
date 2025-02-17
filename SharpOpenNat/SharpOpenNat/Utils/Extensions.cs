//
// Authors:
//   Lucas Ontivero lucasontivero@gmail.com 
//   Luigi Trabacchin <trabacchin.luigi@gmail.com>
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

namespace SharpOpenNat;

/// <summary>
/// Extension Methods for the exposed interfaces
/// </summary>
public static class Extensions
{

    /// <summary>
    /// Get the first available port starting from <paramref name="startingPort" /> on the first found device
    /// </summary>
    public static async Task<int> GetAvailablePortAsync(this INatDiscoverer discoverer, int startingPort, CancellationToken cancellationToken = default)
    {
        var device = await discoverer.DiscoverDeviceAsync(cancellationToken);
        return await device.GetAvailablePortAsync(startingPort, cancellationToken);
    }

    /// <summary>
    /// Get the first available port on the specified <paramref name="device"/> starting from <paramref name="startingPort"/>
    /// </summary>
    public static async Task<int> GetAvailablePortAsync(this INatDevice device, int startingPort, CancellationToken cancellationToken = default)
    {
        var portArray = await device.GetUsedPortsAsync(cancellationToken);
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
    /// Get all used ports on the first found device
    /// </summary>
    public static async Task<List<int>> GetUsedPortsAsync(this INatDiscoverer discoverer, CancellationToken cancellationToken = default)
    {
        var device = await discoverer.DiscoverDeviceAsync(cancellationToken);
        return await device.GetUsedPortsAsync(cancellationToken);
    }

    /// <summary>
    /// Get all used ports on the specified <paramref name="device"/>
    /// </summary>
    public static async Task<List<int>> GetUsedPortsAsync(this INatDevice device, CancellationToken cancellationToken = default)
    {
        var portArray = new List<int>();

        foreach (var mapping in await device.GetAllMappingsAsync(cancellationToken))
        {
            portArray.Add(mapping.PrivatePort);
            portArray.Add(mapping.PublicPort);
        }

        portArray.Sort();

        return portArray;
    }
}
