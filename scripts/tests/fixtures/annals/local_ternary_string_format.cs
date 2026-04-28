public class local_ternary_string_format
{
    public void Generate()
    {
        var place = "Bethesda Susa";
        var value = flag
            ? string.Format("Remember the Battle of {0} in celebration.", place)
            : string.Format("Remember the Battle of {0} in woe.", place);
        SetEventProperty("tombInscription", value);
    }
}
