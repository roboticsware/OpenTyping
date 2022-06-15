using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenTyping
{
    public interface IRank
    {
        Task<List<User>> GetUsersAsync();
        Task<int> AddAsync(User user);
    }
}