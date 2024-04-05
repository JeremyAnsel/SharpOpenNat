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
}
