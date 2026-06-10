using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace QudJP;

internal sealed class ReferenceIdentityComparer : IEqualityComparer<object>
{
    internal static readonly ReferenceIdentityComparer Instance = new();

    private ReferenceIdentityComparer()
    {
    }

    public new bool Equals(object? x, object? y)
    {
        return ReferenceEquals(x, y);
    }

    public int GetHashCode(object obj)
    {
        return RuntimeHelpers.GetHashCode(obj);
    }
}
