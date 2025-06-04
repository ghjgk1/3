namespace Application.Interfaces
{
    public interface IDirectorySearcher : IDisposable
    {
        string Filter { get; set; }
        void SetPropertiesToLoad(string[] properties); 
        IDirectorySearchResult? FindOne();
    }
}
