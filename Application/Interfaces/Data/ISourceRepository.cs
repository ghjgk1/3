using Domain;

namespace Application.Interfaces.Data
{
    public interface ISourceRepository
    {
        Task<IEnumerable<User>> GetUsersFromSourceAsync();
    }
}
