namespace XRL.Wish
{
    public sealed class WishCommand
    {
        public void Run(GameObject ParentObject)
        {
            ParentObject.EmitMessage("Wish debug text");
        }
    }
}
