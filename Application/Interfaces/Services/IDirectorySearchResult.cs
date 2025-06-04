namespace Application.Interfaces
{
    public interface IDirectorySearchResult
    {
        IDirectoryEntry GetDirectoryEntry();
        IPropertyCollection Properties { get; }
    }
}
