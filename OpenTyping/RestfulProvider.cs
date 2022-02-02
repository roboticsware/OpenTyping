using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace OpenTyping
{
    public class RestfulProvider
    {
        private string _baseUrl;
        private string _jwt;
        public List<User> users;

        public RestfulProvider()
        {
            _baseUrl = "http://api.roboticsware.uz:30001";
        }

        public bool OpenDatabase()
        {
            string endpoint = this._baseUrl + "/auth";
            string method = "POST";
            string json = JsonConvert.SerializeObject(new
            {
                username = "RoboticsWare",
                password = "YourPwd"
            });

            try
            {
                WebClient wc = new WebClient();
                wc.Headers["Content-Type"] = "application/json";
                string response = wc.UploadString(endpoint, method, json);
                Dictionary<string, dynamic> convertedRes = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(response);
                this._jwt = convertedRes["token"];
            }
            catch (Exception)
            {
                return false;
            }

            return true;
        }

        public async Task<List<User>> GetUsersAsync()
        {
            Uri endpoint = new Uri(this._baseUrl + "/users");

            WebClient wc = new WebClient();
            wc.Headers["Content-Type"] = "application/json";
            wc.Headers["Authorization"] = "Bearer " + this._jwt;
            
            string response = await wc.DownloadStringTaskAsync(endpoint);
            Dictionary<string, dynamic> convertedRes = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(response);
            users = JsonConvert.DeserializeObject<List<User>>(convertedRes["data"]);
            return users;
        }

        public async Task<string> AddAsync(User user)
        {
            Uri endpoint = new Uri(this._baseUrl + "/users");
            string method = "POST";

            WebClient wc = new WebClient();
            wc.Headers["Content-Type"] = "application/json";
            wc.Headers["Authorization"] = "Bearer " + this._jwt;
            string json = JsonConvert.SerializeObject(user);

            return await wc.UploadStringTaskAsync(endpoint, method, json);
        }
    }
}
