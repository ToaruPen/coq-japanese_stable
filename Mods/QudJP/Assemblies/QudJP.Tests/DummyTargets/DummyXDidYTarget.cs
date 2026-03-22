namespace QudJP.Tests.DummyTargets;

internal static class DummyXDidYTarget
{
    public static bool OriginalExecuted { get; private set; }

    public static void Reset()
    {
        OriginalExecuted = false;
    }

    public static void XDidY(
        object? Actor,
        string Verb,
        string? Extra = null,
        string? EndMark = null,
        string? SubjectOverride = null,
        string? Color = null,
        object? ColorAsGoodFor = null,
        object? ColorAsBadFor = null,
        bool UseFullNames = false,
        bool IndefiniteSubject = false,
        object? SubjectPossessedBy = null,
        object? Source = null,
        bool DescribeSubjectDirection = false,
        bool DescribeSubjectDirectionLate = false,
        bool AlwaysVisible = false,
        bool FromDialog = false,
        bool UsePopup = false,
        object? UseVisibilityOf = null)
    {
        _ = Actor;
        _ = Verb;
        _ = Extra;
        _ = EndMark;
        _ = SubjectOverride;
        _ = Color;
        _ = ColorAsGoodFor;
        _ = ColorAsBadFor;
        _ = UseFullNames;
        _ = IndefiniteSubject;
        _ = SubjectPossessedBy;
        _ = Source;
        _ = DescribeSubjectDirection;
        _ = DescribeSubjectDirectionLate;
        _ = AlwaysVisible;
        _ = FromDialog;
        _ = UsePopup;
        _ = UseVisibilityOf;
        OriginalExecuted = true;
    }

    public static void XDidYToZ(
        object? Actor,
        string Verb,
        string? Preposition = null,
        object? Object = null,
        string? Extra = null,
        string? EndMark = null,
        string? SubjectOverride = null,
        string? Color = null,
        object? ColorAsGoodFor = null,
        object? ColorAsBadFor = null,
        bool UseFullNames = false,
        bool IndefiniteSubject = false,
        bool IndefiniteObject = false,
        bool IndefiniteObjectForOthers = false,
        bool PossessiveObject = false,
        object? SubjectPossessedBy = null,
        object? ObjectPossessedBy = null,
        object? Source = null,
        bool DescribeSubjectDirection = false,
        bool DescribeSubjectDirectionLate = false,
        bool AlwaysVisible = false,
        bool FromDialog = false,
        bool UsePopup = false,
        object? UseVisibilityOf = null)
    {
        _ = Actor;
        _ = Verb;
        _ = Preposition;
        _ = Object;
        _ = Extra;
        _ = EndMark;
        _ = SubjectOverride;
        _ = Color;
        _ = ColorAsGoodFor;
        _ = ColorAsBadFor;
        _ = UseFullNames;
        _ = IndefiniteSubject;
        _ = IndefiniteObject;
        _ = IndefiniteObjectForOthers;
        _ = PossessiveObject;
        _ = SubjectPossessedBy;
        _ = ObjectPossessedBy;
        _ = Source;
        _ = DescribeSubjectDirection;
        _ = DescribeSubjectDirectionLate;
        _ = AlwaysVisible;
        _ = FromDialog;
        _ = UsePopup;
        _ = UseVisibilityOf;
        OriginalExecuted = true;
    }

    public static void WDidXToYWithZ(
        object? Actor,
        string Verb,
        string? DirectPreposition,
        object? DirectObject,
        string? IndirectPreposition,
        object? IndirectObject,
        string? Extra = null,
        string? EndMark = null,
        string? SubjectOverride = null,
        string? Color = null,
        object? ColorAsGoodFor = null,
        object? ColorAsBadFor = null,
        bool UseFullNames = false,
        bool IndefiniteSubject = false,
        bool IndefiniteDirectObject = false,
        bool IndefiniteIndirectObject = false,
        bool IndefiniteDirectObjectForOthers = false,
        bool IndefiniteIndirectObjectForOthers = false,
        bool PossessiveDirectObject = false,
        bool PossessiveIndirectObject = false,
        object? SubjectPossessedBy = null,
        object? DirectObjectPossessedBy = null,
        object? IndirectObjectPossessedBy = null,
        object? Source = null,
        bool DescribeSubjectDirection = false,
        bool DescribeSubjectDirectionLate = false,
        bool AlwaysVisible = false,
        bool FromDialog = false,
        bool UsePopup = false,
        object? UseVisibilityOf = null)
    {
        _ = Actor;
        _ = Verb;
        _ = DirectPreposition;
        _ = DirectObject;
        _ = IndirectPreposition;
        _ = IndirectObject;
        _ = Extra;
        _ = EndMark;
        _ = SubjectOverride;
        _ = Color;
        _ = ColorAsGoodFor;
        _ = ColorAsBadFor;
        _ = UseFullNames;
        _ = IndefiniteSubject;
        _ = IndefiniteDirectObject;
        _ = IndefiniteIndirectObject;
        _ = IndefiniteDirectObjectForOthers;
        _ = IndefiniteIndirectObjectForOthers;
        _ = PossessiveDirectObject;
        _ = PossessiveIndirectObject;
        _ = SubjectPossessedBy;
        _ = DirectObjectPossessedBy;
        _ = IndirectObjectPossessedBy;
        _ = Source;
        _ = DescribeSubjectDirection;
        _ = DescribeSubjectDirectionLate;
        _ = AlwaysVisible;
        _ = FromDialog;
        _ = UsePopup;
        _ = UseVisibilityOf;
        OriginalExecuted = true;
    }
}

internal sealed class DummyVisibilityTarget
{
    public DummyVisibilityTarget(bool isPlayer = false, bool isVisible = true, DummyVisibilityTarget? holder = null)
    {
        Player = isPlayer;
        Visible = isVisible;
        Holder = holder;
    }

    public DummyVisibilityTarget? Holder { get; }

    private bool Player { get; }

    private bool Visible { get; }

    public bool IsPlayer()
    {
        return Player;
    }

    public bool IsVisible()
    {
        return Visible;
    }
}
