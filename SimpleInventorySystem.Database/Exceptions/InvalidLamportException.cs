namespace SimpleInventorySystem.Database.Exceptions
{
    [Serializable]
    internal class InvalidLamportException : Exception
    {
        public InvalidLamportException()
        {
        }

        public InvalidLamportException(string? message) : base(message)
        {
        }

        public InvalidLamportException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}