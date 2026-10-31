namespace quillbridge.Application.Guides;

// the fixed structure every guide has, whether written by the AI or by the template
public class GuideContent
{
    public string Opening { get; set; } = string.Empty;
    public string WhyNow { get; set; } = string.Empty;
    public List<string> DiscoveryQuestions { get; set; } = [];
    public string CoreMessage { get; set; } = string.Empty;
    public string SuccessStory { get; set; } = string.Empty;
    public string MeetingAsk { get; set; } = string.Empty;
    public List<GuidePushback> Pushbacks { get; set; } = [];
    public GuidePlanB PlanB { get; set; } = new();
    // empty when we never contacted them
    public string LastTouch { get; set; } = string.Empty;
    // 0-2 facts read from the notes and last-touch text
    public List<string> KeyFacts { get; set; } = [];
}

public class GuidePushback
{
    public string Key { get; set; } = string.Empty;
    public string Objection { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}

public class GuidePlanB
{
    public string Switchboard { get; set; } = string.Empty;
    public string Voicemail { get; set; } = string.Empty;
    public string SendEmail { get; set; } = string.Empty;
    public string Gatekeeper { get; set; } = string.Empty;
    public string WrongPerson { get; set; } = string.Empty;
}
