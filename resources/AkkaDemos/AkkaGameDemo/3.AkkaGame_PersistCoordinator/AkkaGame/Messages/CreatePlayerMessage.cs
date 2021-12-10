namespace AkkaGame.Messages
{
    public class CreatePlayerMessage
    {
        public string PlayerName { get; }

        public CreatePlayerMessage(string playerName)
        {
            PlayerName = playerName;
        }
    }
}