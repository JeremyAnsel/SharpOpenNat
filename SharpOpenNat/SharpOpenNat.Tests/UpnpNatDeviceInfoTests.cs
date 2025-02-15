using SharpOpenNat.Pmp;
using System.Net;

namespace SharpOpenNat.Tests;

[TestClass]
public class UpnpNatDeviceInfoTests
{
    [TestMethod]
    public void X()
    {
        var info = new UpnpNatDeviceInfo(IPAddress.Loopback, new Uri("http://127.0.0.1:3221"), "/control?WANIPConnection", null);
        Assert.AreEqual("http://127.0.0.1:3221/control?WANIPConnection", info.ServiceControlUri.ToString());
    }

    [TestMethod]
    public void PmpMappingWriter_should_write_the_same_byte_array_as_the_tested_implementation()
    {
        var mapping = new Mapping(Protocol.Tcp, 5000, 6000);
        var create = true;
        var package = new List<byte>
        {
            PmpConstants.Version,
            mapping.Protocol == Protocol.Tcp ? PmpConstants.OperationCodeTcp : PmpConstants.OperationCodeUdp,
            0, //reserved
            0 //reserved
        };

        package.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder((short)mapping.PrivatePort)));
        package.AddRange(BitConverter.GetBytes(create ? IPAddress.HostToNetworkOrder((short)mapping.PublicPort) : (short)0));
        package.AddRange(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(mapping.Lifetime)));

        var buffer = new byte[PmpConstants.CreateMappingPackageLength];
        PmpMappingWriter.WriteMapping(buffer, mapping, create);
        CollectionAssert.AreEqual(package, buffer);
    }
}
