namespace XRL.World.Parts.Mutation
{
    public abstract class BaseMutation
    {
        public virtual string GetDescription()
        {
            return "";
        }

        public virtual string GetLevelText(int level)
        {
            return "";
        }
    }

    public class TestMutation : BaseMutation
    {
        public override string GetDescription()
        {
            return "mutation description";
        }

        public override string GetLevelText(int level)
        {
            return "mutation level";
        }
    }
}
