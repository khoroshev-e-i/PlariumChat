namespace Shared
{
    public class Greeting
    {
        public string Username { get; set; }

        public string ToJson()
        {
            return $"{{\"Username\":\"{Username}\"}}";
        }
    }
}