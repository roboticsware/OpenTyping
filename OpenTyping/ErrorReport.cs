﻿using System.IO;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System;

namespace OpenTyping
{
    class ErrorReport
    {
        public static readonly string errorFile = "./errors.log";
        public static readonly string errorServerUri = "http://DESKTOP-OPL53JG:30001/errors";
        public static void SendErrorLogs()
        {
            if (File.Exists(errorFile))
                try
                {
                    using (WebClient wc = new WebClient())
                    {
                        var os = System.Environment.OSVersion;

                        string osInfo =
                            $"\nCurrent OS Information:\n" +
                            $"Platform: {os.Platform}\n" +
                            $"Version: {os.VersionString}\n";

                        File.AppendAllText(errorFile, osInfo);

                        byte[] response = wc.UploadFile(new Uri(errorServerUri), "POST", errorFile);

                        string resultString = Encoding.ASCII.GetString(response);

                        Dictionary<string, dynamic> convertedRes =
                            JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(resultString);

                        bool logSentSuccesfully = convertedRes["succesful"];

                        if (logSentSuccesfully)
                            File.Delete(errorFile);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.Write(ex.Message);
                    File.AppendAllText(errorFile, ex.Message);
                }
        }
    }
}

