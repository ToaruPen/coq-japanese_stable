using System.Collections.Generic;
using System.Runtime.Serialization;

namespace QudJP.Tests.L1;

[DataContract]
internal sealed class JournalPatternDocumentDto
{
    [DataMember(Name = "entries")]
    public List<JournalPatternEntryDto>? Entries { get; set; }

    [DataMember(Name = "patterns")]
    public List<JournalPatternEntryDto>? Patterns { get; set; }
}

[DataContract]
internal sealed class JournalPatternEntryDto
{
    [DataMember(Name = "pattern")]
    public string? Pattern { get; set; }

    [DataMember(Name = "template")]
    public string? Template { get; set; }
}
