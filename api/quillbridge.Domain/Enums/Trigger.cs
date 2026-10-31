namespace quillbridge.Domain.Enums;

public enum Trigger
{
    // Reverted in order to get more points in ordering
    None,
    // opening new sites or entering new countries
    Expansion,
    // bought or merged with another company
    Acquisition,
    // facing an outside check of their accounts
    Audit,
    // hiring invoice staff
    Hiring,
    // raised money from investors
    Funding,
    // new head of finance
    NewLeader,
    // switching accounting software
    NewSystem
}