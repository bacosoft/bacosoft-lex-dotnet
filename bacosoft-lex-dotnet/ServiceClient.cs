using System;
using System.Net;
using System.IO;
using System.Text;
using System.Security;
using System.Xml;
using System.Net.Sockets;

namespace Bacosoft
{
    public class ServiceClient
    {
        private const string IMPORT = "/lex/api/import";

        public string BaseUrl { get; set; }

        public string Tenant { get; set; }

        public string UserName { get; set; }

        public string Password { get; set; }

        public string Query(string resource, string parameters)
        {
            return GetResponse(resource, parameters);
        }

        public string Import(long? idEmpresa, long? idEstablecimiento, string data, bool validate)
        {
            string elemEmpresa = string.Empty;
            if (idEmpresa.HasValue)
            {
                elemEmpresa = $@"<bodega>{idEmpresa.Value}</bodega>";
            }
            string elemEstablecimiento = string.Empty;
            if (idEstablecimiento.HasValue)
            {
                elemEstablecimiento = $@"<establecimiento>{idEstablecimiento.Value}</establecimiento>";
            }
            string elemData = SecurityElement.Escape(data);
            string body = $@"<ImportDataParameters>{elemEmpresa}{elemEstablecimiento}<data>{elemData}</data><validate>{validate}</validate></ImportDataParameters>";
            return GetResponse(IMPORT, body);
        }

        private string GetResponse(string resource, string body)
        {
            try
            {
                HttpWebRequest req = CreateRequest(resource, body);
                return GetResponse(req);
            }
            catch (WebException e)
            {
                ServiceException serviceEx = CreateServiceException(e);
                if (serviceEx != null)
                {
                    throw serviceEx;
                }
                else
                {
                    throw;
                }
            }
        }

        private static string GetResponse(HttpWebRequest request)
        {
            try
            {
                using (HttpWebResponse response = (HttpWebResponse) request.GetResponse())
                {
                    using (Stream receiveStream = response.GetResponseStream())
                    {
                        using (StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8))
                        {
                            return readStream.ReadToEnd();
                        }
                    }
                }
            }
            catch(WebException e)
            {
                ServiceException serviceEx = CreateServiceException(e);
                if (serviceEx != null)
                {
                    throw serviceEx;
                }
                else
                {
                    throw;
                }
            }
        }

        private static ServiceException CreateServiceException(WebException e)
        {
            ServiceException res = null;
            using (HttpWebResponse response = (HttpWebResponse) e.Response)
            {
                if (response != null)
                {
                    using (Stream receiveStream = response.GetResponseStream())
                    {
                        using (StreamReader readStream = new StreamReader(receiveStream, Encoding.UTF8))
                        {
                            res = CreateServiceException(response.StatusCode, readStream.ReadToEnd());
                        }
                    }
                }
                else
                {
                    res = new NetworkException(e.Message, e.InnerException);
                }
            }
            return res;
        }

        private static ServiceException CreateServiceException(HttpStatusCode statusCode, string error)
        {
            ServiceException res = null;
            try
            {
                string message = null;
                DateTime timestamp = DateTime.Now;

                XmlDocument xdoc = new XmlDocument();
                xdoc.LoadXml(error);
                foreach (XmlNode node in xdoc.DocumentElement.ChildNodes)
                {
                    if ("timestamp".Equals(node.LocalName))
                    {
                        timestamp = DateTime.Parse(node.InnerText);
                    }
                    else if ("message".Equals(node.LocalName))
                    {
                        message = node.InnerText;
                    }
                }

                res = new ServiceException(statusCode, timestamp, message);
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine(e);
            }
            return res;
        }

        private static string GetTenantString(string tenant)
        {
            return "?tenant=" + tenant;
        }

        private static string GetCredentials(string userName, string password)
        {
            string encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(userName + ":" + password));
            return "Basic " + encoded;
        }

        private HttpWebRequest CreateRequest(string resource, string body)
        {
            if (ServicePointManager.SecurityProtocol < SecurityProtocolType.Tls12)
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(BaseUrl + resource + GetTenantString(Tenant));
            request.Method = "POST";
            request.Headers.Add(HttpRequestHeader.Authorization, GetCredentials(UserName, Password));
            request.Accept = "application/xml";
            request.ContentType = "application/xml";

            using (var streamWriter = new StreamWriter(request.GetRequestStream()))
            {
                streamWriter.Write(body);
                streamWriter.Flush();
                streamWriter.Close();
            }
            return request;
        }
    }
}
