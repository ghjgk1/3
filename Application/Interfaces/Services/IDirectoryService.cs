namespace Application.Interfaces
{
    public interface IDirectoryService
    {
        IDirectoryEntry GetDirectoryEntry(string path, string username, string password);
        IDirectorySearcher CreateSearcher(IDirectoryEntry directoryEntry);
    }
}
