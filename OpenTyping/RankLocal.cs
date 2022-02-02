using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenTyping
{
    public class RankLocal : IRank
    {
        private List<User> users;
        private readonly SqliteProvider Sqlite = new SqliteProvider();

        public RankLocal() { }

        public async Task<List<User>> GetUsersAsync()
        {
            if (await Sqlite.OpenDatabase())
            {
                users = await Sqlite.GetUsersAsync();
                if (users.Count == 0) // If first insertion
                {
                    users = new List<User>();
                }
                users.Sort();

                return users;
            }

            return null;
        }

        public async Task<int> AddSync(User user)
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

            await Sqlite.ReWriteAllAsync(users);
            return curPos;
        }
    }
}