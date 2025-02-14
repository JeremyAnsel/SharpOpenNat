//
// Authors:
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

using System.Diagnostics;
using System.Net;

namespace SharpOpenNat.Pmp
{
    internal static class PmpMappingWriter
    {
        public static void WriteMapping(byte[] buffer, Mapping mapping, bool create)
        {
            Debug.Assert(buffer.Length > 11);

#if NET6_0_OR_GREATER
            buffer[0] = PmpConstants.Version;
            buffer[1] = mapping.Protocol == Protocol.Tcp ? PmpConstants.OperationCodeTcp : PmpConstants.OperationCodeUdp;
            buffer[2] = (Byte)0; //reserved
            buffer[3] = (Byte)0; //reserved

            BitConverter.TryWriteBytes(new Span<Byte>(buffer, 4, 2), IPAddress.HostToNetworkOrder((short)mapping.PrivatePort));
            BitConverter.TryWriteBytes(new Span<Byte>(buffer, 6, 2), create ? IPAddress.HostToNetworkOrder((short)mapping.PublicPort) : (short)0);
            BitConverter.TryWriteBytes(new Span<Byte>(buffer, 8, 4), IPAddress.HostToNetworkOrder(mapping.Lifetime));
#else

            using (var memoryStream = new MemoryStream(buffer))
            using (var streamWriter = new BinaryWriter(memoryStream))
            {
                streamWriter.Write(PmpConstants.Version);
                streamWriter.Write(mapping.Protocol == Protocol.Tcp ? PmpConstants.OperationCodeTcp : PmpConstants.OperationCodeUdp);
                streamWriter.Write((Byte)0); //reserved
                streamWriter.Write((Byte)0); //reserved

                streamWriter.Write(IPAddress.HostToNetworkOrder((short)mapping.PrivatePort));
                streamWriter.Write(create ? IPAddress.HostToNetworkOrder((short)mapping.PublicPort) : (short)0);
                streamWriter.Write(IPAddress.HostToNetworkOrder(mapping.Lifetime));
            }
#endif
        }
    }
}
