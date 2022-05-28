using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;

namespace OpenTyping
{
    class InternetServices
    {
        public static bool IsConnected()
        {
            try
            {
                Ping myPing = new Ping();
                String host = "google.com";
                byte[] buffer = new byte[32];
                int timeout = 1000;
                PingOptions pingOptions = new PingOptions();
                PingReply reply = myPing.Send(host, timeout, buffer, pingOptions);
                return (reply.Status == IPStatus.Success);
            }
            catch (Exception)
            {
                return false;
            }
        }
        public static void SendLogsToServer()
        {
            using (WebClient client = new WebClient())
            {
                using (Stream fileStream = File.OpenRead("./errors.log"))
                using (Stream requestStream = client.OpenWrite(new Uri("http://Zafar:30001/errors"), "POST"))
                {
                    fileStream.CopyTo(requestStream);
                }
            }
        }
    }
}
