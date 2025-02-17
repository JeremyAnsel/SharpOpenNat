//
// Authors:
//   Ben Motmans <ben.motmans@gmail.com>
//
// Copyright (C) 2007 Ben Motmans
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

using SharpOpenNat;

var t = Task.Run(async () =>
{
    //var usedPorts = await OpenNat.Discoverer.GetUsedPortsAsync();
    //Console.WriteLine(1600 + " -> " + await OpenNat.Discoverer.GetAvailablePortAsync(1600));
    //Console.WriteLine(1700 + " -> " + await OpenNat.Discoverer.GetAvailablePortAsync(1700));
    //Console.WriteLine(1601 + " -> " + await OpenNat.Discoverer.GetAvailablePortAsync(1601));
    //Console.WriteLine(1701 + " -> " + await OpenNat.Discoverer.GetAvailablePortAsync(1701));
    //Console.WriteLine(1602 + " -> " + await OpenNat.Discoverer.GetAvailablePortAsync(1602));
    //Console.WriteLine(1702 + " -> " + await OpenNat.Discoverer.GetAvailablePortAsync(1702));
    //Console.WriteLine(1603 + " -> " + await OpenNat.Discoverer.GetAvailablePortAsync(1603));
    //Console.WriteLine(1703 + " -> " + await OpenNat.Discoverer.GetAvailablePortAsync(1703));

    using var cts = new CancellationTokenSource(5000);
    var device = await OpenNat.Discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts.Token);

    var ip = await device.GetExternalIPAsync();
    Console.Write("\nYour IP: {0}", ip);

    await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 1600, 1700, "SharpOpenNat (temporary)"));
    //await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 1601, 1701, "SharpOpenNat (Session lifetime)"));
    //await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 1602, 1702, 0, "SharpOpenNat (Permanent lifetime)"));
    await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 1603, 1703, 20, "SharpOpenNat (Manual lifetime)"));

    Console.Write("\nAdded mapping: {0}:1700 -> 127.0.0.1:1600\n", ip);
    Console.Write("\n+------+-------------------------------+--------------------------------+------------------------------------+-------------------------+");
    Console.Write("\n| PROT | PUBLIC (Reacheable)           | PRIVATE (Your computer)        | Description                        |                         |");
    Console.Write("\n+------+----------------------+--------+-----------------------+--------+------------------------------------+-------------------------+");
    Console.Write("\n|      | IP Address           | Port   | IP Address            | Port   |                                    | Expires                 |");
    Console.Write("\n+------+----------------------+--------+-----------------------+--------+------------------------------------+-------------------------+");
    foreach (var mapping in await device.GetAllMappingsAsync())
    {
        Console.Write("\n|  {5} | {0,-20} | {1,6} | {2,-21} | {3,6} | {4,-35}|{6,25}|",
            ip, mapping.PublicPort, mapping.PrivateIP, mapping.PrivatePort, mapping.Description,
            mapping.Protocol == Protocol.Tcp ? "TCP" : "UDP", mapping.Expiration.ToLocalTime());
    }
    Console.Write("\n+------+----------------------+--------+-----------------------+--------+------------------------------------+-------------------------+");

    Console.Write("\n");
    Console.Write("\n[Removing TCP mapping] {0}:1700 -> 127.0.0.1:1600", ip);
    await device.DeletePortMapAsync(new Mapping(Protocol.Tcp, 1600, 1700));
    Console.Write("\n[Done]");

    var mappings = await device.GetAllMappingsAsync();
    var deleted = mappings.All(x => x.Description != "SharpOpenNat Testing");
    Console.WriteLine(deleted
        ? "[SUCCESS]: Test mapping effectively removed ;)"
        : "[FAILURE]: Test mapping wan not removed!");
});

try
{
    t.Wait();
}
catch (AggregateException e)
{
    if (e.InnerException is NatDeviceNotFoundException)
    {
        Console.WriteLine("\nNot found");
    }
    else
    {
        Console.WriteLine("\n" + e.InnerException);
    }
}

Console.WriteLine("\nPress any key to exit...");
Console.ReadKey();
