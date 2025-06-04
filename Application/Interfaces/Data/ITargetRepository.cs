using Domain;

namespace Application.Interfaces.Data
{
    public interface ITargetRepository
    {
        User? FindUserInTarget(string identifier);
        void UpdateUserInTarget(User user);
    }
}
