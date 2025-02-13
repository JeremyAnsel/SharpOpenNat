//
// Authors:
//   Lucas Ontivero lucasontivero@gmail.com
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

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;
using System.Xml;

namespace SharpOpenNat;

internal class SoapClient
{
    private readonly string? _serviceType;
    private readonly Uri _url;

    public SoapClient(Uri url, string? serviceType)
    {
        _url = url;
        _serviceType = serviceType;
    }

    public async Task<XmlDocument> InvokeAsync(string operationName, IDictionary<string, object> args)
    {
        OpenNat.TraceSource.TraceEvent(TraceEventType.Verbose, 0, "SOAPACTION: **{0}** url:{1}", operationName,
                                             _url);
        byte[] messageBody = BuildMessageBody(operationName, args);
        HttpWebRequest request = BuildHttpWebRequest(operationName, messageBody);

        if (messageBody.Length > 0)
        {
            using var stream = await request.GetRequestStreamAsync();

#if NET6_0_OR_GREATER
            await stream.WriteAsync(messageBody);
#else
            await stream.WriteAsync(messageBody, 0, messageBody.Length);
#endif
        }

        using var response = await GetWebResponse(request);
        string? responseBody = null;

        if (response is not null)
        {
            var stream = response.GetResponseStream();
            var contentLength = response.ContentLength;

            var reader = new StreamReader(stream, Encoding.UTF8);

            responseBody = contentLength != -1
                                ? reader.ReadAsMany((int)contentLength)
                                : reader.ReadToEnd();
        }

        var responseXml = SoapClient.GetXmlDocument(responseBody);

        response?.Close();
        return responseXml;
    }

    private static async Task<WebResponse?> GetWebResponse(WebRequest request)
    {
        WebResponse? response;
        try
        {
            response = await request.GetResponseAsync();
        }
        catch (WebException ex)
        {
            OpenNat.TraceSource.TraceEvent(TraceEventType.Verbose, 0, "WebException status: {0}", ex.Status);

            // Even if the request "failed" we need to continue reading the response from the router
            response = ex.Response as HttpWebResponse;

            if (response == null)
                throw;
        }
        return response;
    }

    private HttpWebRequest BuildHttpWebRequest(string operationName, byte[] messageBody)
    {
#pragma warning disable SYSLIB0014
        var request = WebRequest.CreateHttp(_url);
#pragma warning restore SYSLIB0014
        request.KeepAlive = false;
        request.Method = "POST";
        request.ContentType = "text/xml; charset=\"utf-8\"";
        request.Headers.Add("SOAPACTION", "\"" + _serviceType + "#" + operationName + "\"");
        request.ContentLength = messageBody.Length;
        return request;
    }

    private byte[] BuildMessageBody(string operationName, IEnumerable<KeyValuePair<string, object>> args)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<s:Envelope ");
        sb.AppendLine("   xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" ");
        sb.AppendLine("   s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">");
        sb.AppendLine("   <s:Body>");
        sb.AppendLine("	  <u:" + operationName + " xmlns:u=\"" + _serviceType + "\">");
        foreach (var a in args)
        {
            sb.AppendLine("		 <" + a.Key + ">" + Convert.ToString(a.Value, CultureInfo.InvariantCulture) +
                          "</" + a.Key + ">");
        }
        sb.AppendLine("	  </u:" + operationName + ">");
        sb.AppendLine("   </s:Body>");
        sb.Append("</s:Envelope>\r\n\r\n");
        string requestBody = sb.ToString();

        byte[] messageBody = Encoding.UTF8.GetBytes(requestBody);
        return messageBody;
    }

    private static XmlDocument GetXmlDocument(string? response)
    {
        XmlNode? node;
        var doc = new XmlDocument();

        if (response is not null)
        {
            doc.LoadXml(response);
        }

        var nsm = new XmlNamespaceManager(doc.NameTable);

        // Error messages should be found under this namespace
        nsm.AddNamespace("errorNs", "urn:schemas-upnp-org:control-1-0");

        // Check to see if we have a fault code message.
        if ((node = doc.SelectSingleNode("//errorNs:UPnPError", nsm)) != null)
        {
            int code = Convert.ToInt32(node.GetXmlElementText("errorCode"), CultureInfo.InvariantCulture);
            string errorMessage = node.GetXmlElementText("errorDescription");
            OpenNat.TraceSource.LogWarn("Server failed with error: {0} - {1}", code, errorMessage);
            throw new MappingException(code, errorMessage);
        }

        return doc;
    }
}
