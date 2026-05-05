namespace XRL.World
{
    public static class IComponent
    {
        public static void EmitMessage(GameObject Source, string Message)
        {
            Messaging.EmitMessage(Source, Message);
        }
    }
}
