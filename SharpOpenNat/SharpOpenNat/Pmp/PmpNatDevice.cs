//
// Authors:
//   Ben Motmans <ben.motmans@gmail.com>
//   Lucas Ontivero lucas.ontivero@gmail.com
//   Luigi Trabacchin <trabacchin.luigi@gmail.com>
//
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

using SharpOpenNat.Pmp;
using System.Net;
using System.Net.Sockets;

namespace SharpOpenNat;

internal sealed class PmpNatDevice : NatDevice
{
    public override IPEndPoint HostEndPoint
    {
        get { return _hostEndPoint; }
    }

    public override IPAddress LocalAddress
    {
        get { return _localAddress; }
    }

    private readonly IPEndPoint _hostEndPoint;
    private readonly IPAddress _localAddress;
    private readonly IPAddress _publicAddress;

    internal PmpNatDevice(IPAddress hostEndPointAddress, IPAddress localAddress, IPAddress publicAddress)
    {
        _hostEndPoint = new IPEndPoint(hostEndPointAddress, PmpConstants.ServerPort);
        _localAddress = localAddress;
        _publicAddress = publicAddress;
    }

    public override async Task CreatePortMapAsync(Mapping mapping, CancellationToken cancellationToken)
    {
        await InternalCreatePortMapAsync(mapping, true, cancellationToken)
            .TimeoutAfter(TimeSpan.FromSeconds(4), cancellationToken);
        RegisterMapping(mapping);
    }

    public override async Task DeletePortMapAsync(Mapping mapping, CancellationToken cancellationToken = default)
    {
        await InternalCreatePortMapAsync(mapping, false, cancellationToken)
            .TimeoutAfter(TimeSpan.FromSeconds(4), cancellationToken);
        UnregisterMapping(mapping);
    }

    public override Task<Mapping[]> GetAllMappingsAsync(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public override Task<IPAddress?> GetExternalIPAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => (IPAddress?)_publicAddress)
            .TimeoutAfter(TimeSpan.FromSeconds(4), cancellationToken);
    }

    public override Task<Mapping?> GetSpecificMappingAsync(Protocol protocol, int port, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("NAT-PMP does not specify a way to get a specific port map");
    }

    /// <exception cref="MappingException"></exception>
    private async Task<Mapping> InternalCreatePortMapAsync(Mapping mapping, bool create, CancellationToken cancellationToken)
    {
        var rented = System.Buffers.ArrayPool<Byte>.Shared.Rent(PmpConstants.CreateMappingPackageLength);
        try
        {
            PmpMappingWriter.WriteMapping(rented, mapping, create);

            int attempt = 0;
            int delay = PmpConstants.RetryDelay;

            using var udpClient = new UdpClient();
            CreatePortMapListen(udpClient, mapping);

            while (attempt < PmpConstants.RetryAttempts)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await udpClient.SendAsync(rented, PmpConstants.CreateMappingPackageLength, HostEndPoint);

                attempt++;
                delay *= 2;
                await Task.Delay(delay, cancellationToken);
            }
        }
        catch (Exception e)
        {
            string type = create ? "create" : "delete";
            string message = String.Format("Failed to {0} portmap (protocol={1}, private port={2})",
                                           type,
                                           mapping.Protocol,
                                           mapping.PrivatePort);
            OpenNat.TraceSource.LogError(message);
            var pmpException = e as MappingException;
            throw new MappingException(message, pmpException);
        }
        finally
        {
            System.Buffers.ArrayPool<Byte>.Shared.Return(rented);
        }

        return mapping;
    }

    private void CreatePortMapListen(UdpClient udpClient, Mapping mapping)
    {
        var endPoint = HostEndPoint;

        while (true)
        {
            byte[] data = udpClient.Receive(ref endPoint);

            if (data.Length < 16)
                continue;

            if (data[0] != PmpConstants.Version)
                continue;

            var opCode = (byte)(data[1] & 127);

            var protocol = Protocol.Tcp;
            if (opCode == PmpConstants.OperationCodeUdp)
                protocol = Protocol.Udp;

            short resultCode = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(data, 2));
#pragma warning disable IDE0059
            int epoch = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(data, 4));
#pragma warning restore IDE0059

            short privatePort = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(data, 8));
            short publicPort = IPAddress.NetworkToHostOrder(BitConverter.ToInt16(data, 10));

            var lifetime = (uint)IPAddress.NetworkToHostOrder(BitConverter.ToInt32(data, 12));

            if (privatePort < 0 || publicPort < 0 || resultCode != PmpConstants.ResultCodeSuccess)
            {
                var errors = new[]
                                 {
                                     "Success",
                                     "Unsupported Version",
                                     "Not Authorized/Refused (e.g. box supports mapping, but user has turned feature off)"
                                     ,
                                     "Network Failure (e.g. NAT box itself has not obtained a DHCP lease)",
                                     "Out of resources (NAT box cannot create any more mappings at this time)",
                                     "Unsupported opcode"
                                 };
                throw new MappingException(resultCode, errors[resultCode]);
            }

            if (lifetime == 0) return; //mapping was deleted

            //mapping was created
            //TODO: verify that the private port+protocol are a match
            mapping.PublicPort = publicPort;
            mapping.Protocol = protocol;
            mapping.Expiration = DateTime.Now.AddSeconds(lifetime);
            return;
        }
    }


    public override string ToString()
    {
        return String.Format("Local Address: {0}\nPublic IP: {1}\nLast Seen: {2}",
                             HostEndPoint.Address, _publicAddress, LastSeen);
    }
}
