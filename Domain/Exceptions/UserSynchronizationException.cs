namespace Domain.Exceptions
{
    public class UserSynchronizationException : Exception
    {
        public UserSynchronizationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
