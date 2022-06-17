using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace OpenTyping
{
    public class RestfulProvider
    {
        private readonly string baseUrl = "https://your_server_url";
        private string jwtToken;
        private static HttpClient client;
        public List<User> users;

        public RestfulProvider()
        {
            if (RestfulProvider.client == null) // one time initialization is enough
            {
                client = new HttpClient();
                client.BaseAddress = new Uri(baseUrl);
                var contentType = new MediaTypeWithQualityHeaderValue("application/json");
                client.DefaultRequestHeaders.Accept.Add(contentType);
            }
        }

        public async Task<bool> GetTokenAsync()
        {
            string endpoint = "/auth";
            string stringData = JsonConvert.SerializeObject(new
            {
                username = "YourServer",
                password = "ServerPWD"
            });

            StringContent contentData = new StringContent(stringData, System.Text.Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync(endpoint, contentData);
            string stringJWT = response.Content.ReadAsStringAsync().Result;
            Dictionary<string, dynamic> convertedRes = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(stringJWT);

            if (convertedRes.ContainsKey("error"))
            {
                Debug.WriteLine(JsonConvert.SerializeObject(convertedRes)); // to dump object
                return false;
            }

            jwtToken = convertedRes["token"];
            return true;
        }

        public async Task<List<User>> GetUsersAsync()
        {
            string endpoint = "/users";

            HttpResponseMessage response = await client.GetAsync(endpoint);
            string stringData = await response.Content.ReadAsStringAsync();
            Dictionary<string, dynamic> convertedRes = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(stringData);

            if (convertedRes.ContainsKey("error"))
            {
                Debug.WriteLine(JsonConvert.SerializeObject(convertedRes)); // to dump object
                return null;
            }
           
            users = convertedRes["data"].ToObject<List<User>>();
            return users;
        }

        public async Task<string> AddAsync(User user)
        {
            string endpoint = "/users";
            if (await GetTokenAsync())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);

                string stringData = JsonConvert.SerializeObject(new Dictionary<string, User> { { "user", user } });
                StringContent contentData = new StringContent(stringData, System.Text.Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(endpoint, contentData);
                string responseStr = await response.Content.ReadAsStringAsync();
                Dictionary<string, dynamic> convertedRes = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(responseStr);

                if (convertedRes.ContainsKey("error"))
                {
                    Debug.WriteLine(JsonConvert.SerializeObject(convertedRes)); // to dump object
                    if (convertedRes["error"] == "Unauthorized") // wrong jwt token error
                    {
                        App.logger.Error(endpoint + " : " +JsonConvert.SerializeObject(convertedRes)); // leave to file
                        throw new Exception("Auth fail");
                    }
                    return null;
                }
                return convertedRes["data"];
            }
            else
            {
                return null;
            }
        }

        public async Task<bool> SendErrorData(string filePath)
        {
            string endpoint = "/errors";
            if (await GetTokenAsync())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwtToken);

                using (var multipartFormContent = new MultipartFormDataContent())
                {
                    var fileStreamContent = new StreamContent(File.OpenRead(filePath));
                    fileStreamContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
                    multipartFormContent.Add(fileStreamContent, name: "file", fileName: Path.GetFileName(filePath));

                    HttpResponseMessage response = await client.PostAsync(endpoint, multipartFormContent);
                    string responseStr = await response.Content.ReadAsStringAsync();
                    Dictionary<string, dynamic> convertedRes = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(responseStr);

                    if (convertedRes.ContainsKey("error"))
                    {
                        Debug.WriteLine(JsonConvert.SerializeObject(convertedRes)); // to dump object
                        return false;
                    }
                    return true;
                }
            }
            else
            {
                return false;
            }
        }
    }
}
