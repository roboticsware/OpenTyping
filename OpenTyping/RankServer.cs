using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace OpenTyping
{
    public class RankServer : IRank
    {
        private List<User> users;
        private readonly RestfulProvider Restful = new RestfulProvider();

        public RankServer() { }

        public async Task<List<User>> GetUsersAsync()
        {
            try
            {
                users = await Restful.GetUsersAsync();
                if (users.Count == 0) // If first insertion
                {
                    users = new List<User>();
                }
                users.Sort();

                return users;
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Unable to connect"))
                {
                    Debug.WriteLine("Failed to get users from Server. Server is unavailable!");
                    throw new Exception("Server unavailable");
                }
                else if (ex.Message.Contains("SSL/TLS"))
                {
                    Debug.WriteLine("Failed to get users from Server. Server is unavailable!");
                    throw new Exception("Server unavailable");
                }
                else if (ex.Message.Contains("could not be resolved"))
                {
                    Debug.WriteLine("Failed to get users from Server. Network is unavailable!");
                    throw new Exception("Network unavailable");
                }
            }

            return null;
        }

        public async Task<int> AddAsync(User user)
        {
            users.Add(user);
            users.Sort();

            int curPos = users.FindIndex(user.Equals);
            if (curPos == 10) // If not new record
            {
                users.RemoveAt(users.Count - 1);
                return -1;
            }

            if (users.Count > 10) // If records are already full
            {
                users.RemoveAt(users.Count - 1); // Remove always 11th last record
            }

            try
            {
                if (await Restful.GetTokenAsync()) // Get a jwt to add
                {
                    await Restful.AddAsync(user);
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("Unavailable"))
                {
                    Debug.WriteLine("Failed to add to Server. Network is unavailable!");
                    throw new Exception("Network unavailable");
                }
                else if (ex.Message.Contains("key"))
                {
                    Debug.WriteLine("Wrong JWT error, Failed to Auth!");
                    throw new Exception("Auth fail");
                }
            }
            
            return curPos;
        }
    }
}