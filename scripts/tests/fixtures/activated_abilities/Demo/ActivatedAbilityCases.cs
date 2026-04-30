namespace Demo
{
    public sealed class ActivatedAbilityCases
    {
        public void Run(GameObject parent, object abilityId, string mutationName, string abilityName, ActivatedAbilities activatedAbilities, ActivatedAbilityEntry entry, string zoneName)
        {
            // AddActivatedAbility("Commented ability", "CommandCommented");
            string ignored = "AddMyActivatedAbility(\"String literal ability\", \"CommandString\")";
            Guid AddMyActivatedAbility(string Name, string command) => Guid.Empty;


            AddActivatedAbility("Static Punch", "CommandStaticPunch");
            AddMyActivatedAbility(@"Static ""Blink""", "CommandBlink");
            AddActivatedAbility(parent.GetDisplayName(), "CommandParentDisplay");
            AddMyActivatedAbility((GetDisplayName()), "CommandSelfDisplay");
            AddActivatedAbility("Phase " + mutationName, "CommandDynamicConcat");
            AddActivatedAbility($"{parent.GetDisplayName()} Beam", "CommandInterpolated");
            AddMyActivatedAbility(abilityName, "CommandIdentifier");
            SetActivatedAbilityDisplayName(abilityId, BuildAbilityName(parent));
            SetActivatedAbilityDisplayName(abilityId, DisplayName: parent.GetDisplayName());
            AddActivatedAbility(DisplayName: "Named Static", Command: "CommandNamed");
            AddActivatedAbility(GetDisplayName() + Other(), "CommandComposedInvocation");
            AddActivatedAbility(GetDisplayName().Strip(), "CommandChainedInvocation");
            AddActivatedAbility(Name: "Named Name Static", Command: "CommandNamedName");
            AddMyActivatedAbility(Name: BuildAbilityName(parent), Command: "CommandMyNamedName");
            Func<Guid> f = () => AddActivatedAbility("Lambda Static", "CommandLambda");
            string commandId;
            AddDynamicCommand(out commandId, "CommandRecomposite", "Recomposite", "Cybernetics");
            AddDynamicCommand(out commandId, "CommandNamed", Name: "Named Dynamic Command", Class: "Cybernetics");
            AddDynamicCommand(out commandId, CommandForDescription: "CommandMixed", "Mixed Positional Name", "Cybernetics");
            SetMyActivatedAbilityDisplayName(abilityId, "Set My Static");
            SetMyActivatedAbilityDisplayName(abilityId, DisplayName: parent.GetDisplayName());
            activatedAbilities.AddAbility("Eject", "CommandSeatEject", "Vehicle");
            parent.ActivatedAbilities.AddAbility(Name: "Direct Named AddAbility", Command: "CommandDirect", Class: "Maneuvers");
            entry.DisplayName = "Assignment Static";
            entry.DisplayName = "Recoil to " + zoneName;
        }

        public Guid AddActivatedAbility(string Name, string command)
        {
            return Guid.Empty;
        }

        private string GetDisplayName()
        {
            return "fixture";
        }

        private string Other()
        {
            return "other";
        }

        private string BuildAbilityName(GameObject parent)
        {
            return parent.GetDisplayName() + " Burst";
        }
    }

    public interface IActivatedAbilityDeclarations
    {
        Guid? AddActivatedAbility(string Name, string Command);
    }

    public sealed class GameObject
    {
        public ActivatedAbilities ActivatedAbilities;

        public string GetDisplayName()
        {
            return "object";
        }
    }

    public sealed class ActivatedAbilities
    {
        public Guid AddAbility(string Name, string Command, string Class)
        {
            return Guid.Empty;
        }
    }

    public sealed class ActivatedAbilityEntry
    {
        public string DisplayName;
    }
}
