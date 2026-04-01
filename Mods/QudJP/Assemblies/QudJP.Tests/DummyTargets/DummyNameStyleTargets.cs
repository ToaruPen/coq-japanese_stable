using System;
using System.Text;

namespace QudJP.Tests.DummyTargets;

internal static class DummyNameStyleTarget
{
    public static string Generate(bool twoNames = false, string? template = null)
    {
        var nameBuilder = new StringBuilder();
        var nameCount = twoNames ? 2 : 1;

        for (var index = 0; index < nameCount; index++)
        {
            nameBuilder.Append("pre");
            nameBuilder.Append('-');
            nameBuilder.Append("mid");
            nameBuilder.Append('-');
            nameBuilder.Append("post");
            nameBuilder.Append('-');
            nameBuilder.Append("end");

            if (index < nameCount - 1)
            {
                nameBuilder.Append(' ');
            }
        }

        var generated = nameBuilder.ToString().Trim();
        return string.IsNullOrEmpty(template)
            ? generated
            : template.Replace("*Name*", generated, StringComparison.Ordinal);
    }
}
