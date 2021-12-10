namespace AkkaGame.Messages
{
    public enum ExceptionType
    {
        Application,
        IO
    }

    public class CauseErrorMessage
    {
        public readonly ExceptionType ExceptionType;

        public CauseErrorMessage(ExceptionType exceptionType)
        {
            ExceptionType = exceptionType;
        }
    }
}