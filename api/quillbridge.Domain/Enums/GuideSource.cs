namespace quillbridge.Domain.Enums;

public enum GuideSource
{
    Ai,
    // rule-based fallback; never a final cache hit
    Template
}
