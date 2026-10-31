using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using quillbridge.Application.Interfaces;

namespace quillbridge.Application.Guides;

public class ClaudeGuideWriter : IAiGuideWriter
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly AnthropicClient? _client;
    private readonly ClaudeOptions _options;
    private readonly ILogger<ClaudeGuideWriter> _logger;

    public ClaudeGuideWriter(IOptions<ClaudeOptions> options, ILogger<ClaudeGuideWriter> logger)
    {
        _options = options.Value;
        _logger = logger;

        var apiKey = string.IsNullOrWhiteSpace(_options.ApiKey) ? Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") : _options.ApiKey;

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _client = new AnthropicClient
            {
                ApiKey = apiKey,
                Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds),
                MaxRetries = 2
            };
        }
    }

    public bool IsConfigured => _client is not null;

    public string Model => _options.Model;

    public async Task<AiGuideResult> WriteAsync(GuideInput input, IReadOnlyList<GuideExample> examples, IReadOnlyList<string> previousErrors, CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            return new AiGuideResult(null, 0, 0, "No Claude API key set (Claude:ApiKey or ANTHROPIC_API_KEY).");
        }

        try
        {
            var response = await _client.Messages.Create(new MessageCreateParams
            {
                Model = _options.Model,
                MaxTokens = _options.MaxTokens,
                System = GuidePrompt.System,
                Messages = [new UserMessageParam { Content = GuidePrompt.UserMessage(input, examples, previousErrors) }],
                OutputConfig = new OutputConfig
                {
                    Effort = Effort.Low,
                    Format = new JsonOutputFormat { Schema = GuidePrompt.Schema(input) }
                }
            }, cancellationToken);

            var inputTokens = response.Usage.InputTokens;
            var outputTokens = response.Usage.OutputTokens;

            if (response.StopReason != "end_turn")
            {
                return new AiGuideResult(null, inputTokens, outputTokens, $"The model stopped early ({response.StopReason}).");
            }

            var json = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));

            try
            {
                var content = JsonSerializer.Deserialize<GuideContent>(json, ReadOptions);

                return content is null
                    ? new AiGuideResult(null, inputTokens, outputTokens, "The model returned no guide.")
                    : new AiGuideResult(content, inputTokens, outputTokens, null);
            }
            catch (JsonException ex)
            {
                return new AiGuideResult(null, inputTokens, outputTokens, $"The model returned invalid JSON: {ex.Message}");
            }
        }
        catch (AnthropicRateLimitException)
        {
            return new AiGuideResult(null, 0, 0, "Claude API rate limit reached.");
        }
        catch (Anthropic5xxException ex)
        {
            _logger.LogWarning(ex, "Claude API server error.");

            return new AiGuideResult(null, 0, 0, "Claude API is unavailable right now.");
        }
        catch (AnthropicApiException ex)
        {
            // 4xx other than rate limits: bad key, out of credit, bad model name. Never log the key.
            _logger.LogWarning("Claude API rejected the request: {Message}", ex.Message);

            return new AiGuideResult(null, 0, 0, $"Claude API rejected the request: {ex.Message}");
        }
        catch (Exception ex) when (ex is AnthropicIOException or TaskCanceledException or HttpRequestException)
        {
            return new AiGuideResult(null, 0, 0, "Claude API timed out or could not be reached.");
        }
    }
}
