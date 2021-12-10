namespace AkkaGame.Messages
{
    public class HitMessage
    {
        public int Damage { get; }

        public HitMessage(int damage)
        {
            Damage = damage;
        }
    }
}