namespace QudJP.Tests.DummyTargets;

internal sealed class DummyChargenAttributeDataElement
{
    public string Attribute { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}

internal sealed class DummyQudGenotypeModuleTarget
{
    public object? handleUIEvent(string ID, object? Element)
    {
        if (ID != "BeforeGetBaseAttributes" || Element is not List<DummyChargenAttributeDataElement> attributes)
        {
            return Element;
        }

        attributes.Add(new DummyChargenAttributeDataElement
        {
            Attribute = "STR",
            Description = "Your {{W|Strength}} score determines how effectively you penetrate your opponents' armor with melee attacks, how much damage your melee attacks do, your ability to resist forced movement, and your carry capacity.",
        });

        attributes.Add(new DummyChargenAttributeDataElement
        {
            Attribute = "EGO",
            Description = "Your {{W|Ego}} score determines the potency of your ability to haggle with merchants, and your ability to dominate the wills of other living creatures.",
        });

        return Element;
    }
}
