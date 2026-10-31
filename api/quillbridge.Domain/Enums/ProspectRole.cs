namespace quillbridge.Domain.Enums;

public enum ProspectRole
{
    // title we could not place; gets the general questions
    Other,
    // CFO, Finance Director: decides and signs
    FinanceLeader,
    // Controller, AP Manager: handles invoices every day
    FinanceOperations,
    // checks it works with their systems
    It,
    // office, sales, HR... or no title: the call is about finding the right person
    NonBuyer
}
