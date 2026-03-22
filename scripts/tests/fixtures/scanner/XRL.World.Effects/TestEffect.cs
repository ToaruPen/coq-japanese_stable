namespace XRL.World.Effects
{
    public abstract class Effect
    {
        public virtual string GetDescription()
        {
            return "";
        }

        public virtual string GetDetails()
        {
            return "";
        }
    }

    public class TestEffect : Effect
    {
        public override string GetDescription()
        {
            return "effect description";
        }

        public override string GetDetails()
        {
            return "effect details";
        }
    }
}
