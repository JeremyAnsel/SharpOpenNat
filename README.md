
SharpOpenNat
======

SharpOpenNat is a lightweight and easy-to-use class library to allow port forwarding in NAT devices that support UPNP (Universal Plug & Play) and/or PMP (Port Mapping Protocol).


Build Status
------------

[![Build status](https://ci.appveyor.com/api/projects/status/7i7n7a6xe4jq44bl/branch/main?svg=true)](https://ci.appveyor.com/project/JeremyAnsel/sharpopennat/branch/main)
[![NuGet Version](https://buildstats.info/nuget/SharpOpenNat)](https://www.nuget.org/packages/SharpOpenNat)
![License](https://img.shields.io/github/license/JeremyAnsel/SharpOpenNat)


Goals
-----
NATed computers cannot be reached from outside and this is particularly painful for peer-to-peer or friend-to-friend software.
The main goal is to simplify communication amoung computers behind NAT devices that support UPNP and/or PMP providing a clean
and easy interface to get the external IP address and map ports and helping you to achieve peer-to-peer communication.

+ Tested with .NET  _YES_


How to use?
-----------
With nuget :
> **Install-Package SharpOpenNat**

Go on the [nuget website](https://www.nuget.org/packages/SharpOpenNat/) for more information.


Example
--------

The simplest scenario:

```c#
var device = await NatDiscoverer.DiscoverDeviceAsync();
var ip = await device.GetExternalIPAsync();
Console.WriteLine("The external IP Address is: {0} ", ip);
```

The following piece of code shows a common scenario: It starts the discovery process for a NAT-UPNP device and onces discovered it creates a port mapping. If no device is found before ten seconds, it fails with NatDeviceNotFoundException.

```c#
var cts = new CancellationTokenSource(10000);
var device = await NatDiscoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts);

await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 1600, 1700, "The mapping name"));
```


Documentation
-------------
+ Why Open.NAT? Here you have [ten reasons](https://github.com/lontivero/Open.NAT/wiki/Why-Open.NAT) that make Open.NAT a good candidate for you projects
+ [Visit the Wiki page](https://github.com/lontivero/Open.Nat/wiki)


Development
-----------
You are welcome to contribute code. You can send code both as a patch or a GitHub pull request.


Description     | Value
----------------|----------------
License         | [The MIT License (MIT)](https://github.com/JeremyAnsel/SharpOpenNat/blob/main/LICENSE)
Documentation   | http://jeremyansel.github.io/SharpOpenNat
Source code     | https://github.com/JeremyAnsel/SharpOpenNat
Nuget           | https://www.nuget.org/packages/SharpOpenNat
Build           | https://ci.appveyor.com/project/JeremyAnsel/sharpopennat/branch/main

