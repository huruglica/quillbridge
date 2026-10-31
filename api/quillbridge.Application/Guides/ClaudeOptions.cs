namespace quillbridge.Application.Guides;

// bound from the "Claude" config section
public class ClaudeOptions
{
    public const string SectionName = "Claude";

    // empty in appsettings.json; set in appsettings.Development.json or user secrets.
    // When empty, the ANTHROPIC_API_KEY environment variable is used.
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "claude-haiku-5-5";
    // USD per million tokens for the model above
    public decimal InputPricePerMTok { get; set; } = 0.10m;
    public decimal OutputPricePerMTok { get; set; } = 0.50m;
    // total AI spend after which every guide falls back to the template
    public decimal BudgetUsd { get; set; } = 5m;
    public int MaxTokens { get; set; } = 6000;
    public int TimeoutSeconds { get; set; } = 60;
    public int BatchParallelism { get; set; } = 4;

    public decimal Cost(long inputTokens, long outputTokens)
    {
        return (inputTokens * InputPricePerMTok + outputTokens * OutputPricePerMTok) / 1_000_000m;
    }

    // worst case for one request, reserved against the budget before calling
    public decimal MaxCostPerCall => Cost(8000, MaxTokens);
}
