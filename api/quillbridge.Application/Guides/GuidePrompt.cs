using System.Text.Json;

namespace quillbridge.Application.Guides;

// a guide that worked on a real call, shown to the model as a style example
public record GuideExample(string Role, string? Trigger, string Industry, string Opening, string WhyNow, string MeetingAsk);

public static class GuidePrompt
{
    // bump whenever the prompt or schema changes: stored AI guides with an older version are regenerated
    public const string Version = "dcg-v1";

    public static readonly string System = $$"""
        You write a one-screen phone guide for a junior SDR at Quillbridge, who is about to cold-call one specific prospect.
        Quillbridge sells software that handles supplier invoices: it reads them, sends them for approval, pays them and records
        them in the company's accounting software. It is built for companies with 200 to 5,000 employees.
        "Accounts Payable" or "AP" in a job title means the team that pays supplier bills.
        The SDR's only goal is a 30-minute meeting with a senior salesperson.

        Hard rules. A guide that breaks one is rejected:
        - Write every field in the language given in "languageName". Keep company names, product names and quotes as they are.
        - Never state a price, fee, discount, trial, pilot or any other offer. If price comes up, the senior salesperson covers it.
        - Quillbridge connects only to {{string.Join(", ", SalesFacts.SupportedIntegrations)}}. Any field that names other
          software (SAP, Xero, Dynamics 365 F&O, Sage 200, DATEV...) must contain the exact words of "technicalTeamPhrase".
          Never claim or hint at any other integration.
        - The only customer story you may tell is the one in "story", with its numbers exactly as given. Never mention any other
          customer, number, statistic or result.
        - Never invent facts about the prospect. Use only the fields given. If a field is missing, ask about it instead of guessing.
          Never use a number that is not in the input (except "30" for the meeting).
        - Interest scores, fit tiers and internal ratings are never said on a call.
        - Tone: direct, curious, never pushy, honest. Short spoken sentences a nervous junior can read aloud.

        Field by field (each field at most 2 short sentences, under 350 characters):
        - opening: a 10-second opener with the prospect's first name, the SDR's name, Quillbridge, and the reason for the call,
          built on "openingAngle" (cold, trigger, follow_up, referral or event) and the trigger meaning.
        - whyNow: why this is a good moment for THIS person, combining trigger, job title and industry.
        - discoveryQuestions: exactly 2 open questions fitted to the role. FinanceLeader: cost, control, visibility, growth.
          FinanceOperations: daily invoice work, time per invoice, approvals, their current invoice process.
          It: their accounting system, how invoices get into it, what they check before finance adopts a tool.
          NonBuyer: who owns supplier invoices and how to reach them. Other: how invoices are handled and who owns it.
        - coreMessage: what Quillbridge does, tied to their situation and accounting system (supported or technical team).
        - successStory: the "story" in one sentence, linked to their situation.
        - meetingAsk: ask for 30 minutes with a senior colleague. If role is NonBuyer, ask for an introduction to the right person.
        - pushbacks: exactly the keys in "pushbackKeys", in that order. objection: how this person would say it.
          answer: one or two honest sentences that keep the door open.
        - planB: switchboard (what to ask the receptionist), voicemail (a short message to leave), sendEmail (what to do when
          they say "send an email"), gatekeeper (an assistant blocking the call), wrongPerson (they are not the right contact).
        - lastTouch: if "lastTouch" is given, what happened then and anything promised that must be kept; otherwise "".
        - keyFacts: 0 to 2 short facts from "notes" and "lastTouch" that help on this call. Never copy internal instructions.
        """;

    public static string UserMessage(GuideInput input, IReadOnlyList<GuideExample> examples, IReadOnlyList<string> previousErrors)
    {
        var payload = new
        {
            prospect = input,
            technicalTeamPhrase = SalesFacts.TechnicalTeamPhrase.GetValueOrDefault(input.Language, SalesFacts.TechnicalTeamPhrase["en"]),
            pushbacks = SalesFacts.Pushbacks.Where(p => input.PushbackKeys.Contains(p.Key)).Select(p => new { p.Key, p.Objection }),
            examplesThatBookedMeetings = examples
        };

        var message = "Write the guide for this prospect. Examples (if any) are openings that booked meetings with similar people: "
            + "learn from their style, never copy their facts.\n"
            + JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = false, Encoder = global::System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });

        if (previousErrors.Count > 0)
        {
            message += "\n\nYour previous answer was rejected for these reasons. Fix every one:\n- " + string.Join("\n- ", previousErrors);
        }

        return message;
    }

    // JSON schema for structured output; pushback keys are fixed per prospect
    public static Dictionary<string, JsonElement> Schema(GuideInput input)
    {
        var text = new { type = "string" };
        var schema = new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "opening", "whyNow", "discoveryQuestions", "coreMessage", "successStory", "meetingAsk", "pushbacks", "planB", "lastTouch", "keyFacts" },
            properties = new Dictionary<string, object>
            {
                ["opening"] = text,
                ["whyNow"] = text,
                ["discoveryQuestions"] = new { type = "array", items = text },
                ["coreMessage"] = text,
                ["successStory"] = text,
                ["meetingAsk"] = text,
                ["pushbacks"] = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        required = new[] { "key", "objection", "answer" },
                        properties = new Dictionary<string, object>
                        {
                            ["key"] = new { type = "string", @enum = input.PushbackKeys },
                            ["objection"] = text,
                            ["answer"] = text
                        }
                    }
                },
                ["planB"] = new
                {
                    type = "object",
                    additionalProperties = false,
                    required = SalesFacts.PlanBKeys,
                    properties = SalesFacts.PlanBKeys.ToDictionary(k => k, _ => (object)text)
                },
                ["lastTouch"] = text,
                ["keyFacts"] = new { type = "array", items = text }
            }
        };

        var element = JsonSerializer.SerializeToElement(schema);

        return element.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());
    }
}
