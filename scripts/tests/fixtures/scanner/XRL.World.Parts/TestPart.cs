namespace XRL.World.Parts
{
    public abstract class IPPart
    {
        public virtual bool HandleEvent(GetShortDescriptionEvent e)
        {
            return true;
        }
    }

    public class TestPart : IPPart
    {
        public override bool HandleEvent(GetShortDescriptionEvent e)
        {
            return true;
        }
    }

    public class GetShortDescriptionEvent
    {
    }
}
