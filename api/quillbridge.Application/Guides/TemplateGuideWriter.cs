using System.Text.RegularExpressions;

namespace quillbridge.Application.Guides;

// rule-based guide with the same structure as the AI guide, used when the AI is unavailable, out of
// budget or fails validation. Every sentence is chosen from the prospect's role, trigger, opening angle,
// accounting system and history, so two different people still get different guides.
public static partial class TemplateGuideWriter
{
    public const string Version = "template-v1";

    // Spanish (Sam) and Turkish (Elif) speakers also speak English, so English is the fallback
    public static readonly string[] Languages = ["en", "de", "it"];

    [GeneratedRegex(@"Referred by (?:a manager at )?(?<who>[^.]+)", RegexOptions.IgnoreCase)]
    private static partial Regex ReferralPattern();

    public static string LanguageFor(GuideInput input)
    {
        return Languages.Contains(input.Language) ? input.Language : "en";
    }

    public static GuideContent Write(GuideInput input)
    {
        var t = Texts[LanguageFor(input)];
        var system = input.AccountingSystem ?? "";
        string F(string key) => Fill(t[key], input, t);

        var integration = input.Integration switch
        {
            "supported" => F("integration_supported"),
            "not_supported" => F("integration_not_supported"),
            _ => F("integration_unknown")
        };

        return new GuideContent
        {
            Opening = F($"opening_{input.OpeningAngle}"),
            WhyNow = $"{F($"why_{input.Trigger ?? "None"}")} {F($"why_role_{input.Role}")}",
            DiscoveryQuestions = Questions(input, F),
            CoreMessage = $"{F("core")} {integration}",
            SuccessStory = t[$"story_{input.StoryKey}"],
            MeetingAsk = F(input.Role == "NonBuyer" ? "ask_referral" : "ask"),
            Pushbacks = input.PushbackKeys.Select(key => new GuidePushback
            {
                Key = key,
                Objection = F($"pb_{key}_q"),
                Answer = F(PushbackAnswerKey(key, input))
            }).ToList(),
            PlanB = new GuidePlanB
            {
                Switchboard = F("plan_switchboard"),
                Voicemail = F("plan_voicemail"),
                SendEmail = F("plan_email"),
                Gatekeeper = F("plan_gatekeeper"),
                WrongPerson = F("plan_wrong")
            },
            LastTouch = input.LastTouch is null ? string.Empty : F("last_touch"),
            KeyFacts = input.Notes.Where(n => !Regex.IsMatch(n, "prefer|bevorzugt|preferisce|prefiere|prefere|liever", RegexOptions.IgnoreCase)).Take(2).ToList()
        };
    }

    private static List<string> Questions(GuideInput input, Func<string, string> f)
    {
        return input.Role switch
        {
            "FinanceLeader" => [f("q_leader_1"), f("q_leader_2")],
            "FinanceOperations" => [f(input.InvoiceProcess is null ? "q_ops_1_unknown" : "q_ops_1"), f("q_ops_2")],
            // an unsupported system is never named outside the "technical team" answer
            "It" => [f(input.Integration switch { "supported" => "q_it_1", "not_supported" => "q_it_1_other", _ => "q_it_1_unknown" }), f("q_it_2")],
            "NonBuyer" => [f("q_nonbuyer_1"), f("q_nonbuyer_2")],
            _ => [f("q_other_1"), f("q_other_2")]
        };
    }

    private static string PushbackAnswerKey(string key, GuideInput input)
    {
        return key switch
        {
            "integration" => input.Integration switch
            {
                "supported" => "pb_integration_a_supported",
                "not_supported" => "pb_integration_a_not_supported",
                _ => "pb_integration_a_unknown"
            },
            "switching_accounting" when input.SwitchingTo is not null =>
                input.SwitchingToSupported == true ? "pb_switching_accounting_a_supported" : "pb_switching_accounting_a_not_supported",
            _ => $"pb_{key}_a"
        };
    }

    private static string Fill(string text, GuideInput input, IReadOnlyDictionary<string, string> t)
    {
        var referrer = input.Notes.Select(n => ReferralPattern().Match(n)).FirstOrDefault(m => m.Success)?.Groups["who"].Value;

        return text
            .Replace("{first}", input.FirstName)
            .Replace("{last}", input.LastName)
            .Replace("{title}", input.Title ?? t["their_role"])
            .Replace("{sdr}", input.SdrName)
            .Replace("{company}", input.Company)
            .Replace("{industry}", input.Industry.ToLowerInvariant())
            .Replace("{system}", input.AccountingSystem ?? "")
            .Replace("{switching}", input.SwitchingTo ?? "")
            .Replace("{process}", input.InvoiceProcess ?? "")
            .Replace("{lastTouch}", input.LastTouch ?? "")
            .Replace("{referrer}", referrer ?? t["a_contact"])
            .Replace("{source}", input.LeadSource.ToLowerInvariant());
    }

    private static readonly Dictionary<string, Dictionary<string, string>> Texts = new()
    {
        ["en"] = new()
        {
            ["their_role"] = "the person in charge of supplier invoices",
            ["a_contact"] = "one of our contacts",
            ["opening_cold"] = "Hi {first}, this is {sdr} from Quillbridge. We help finance teams handle supplier invoices. Is now a bad time for a quick question?",
            ["opening_trigger"] = "Hi {first}, {sdr} from Quillbridge. I'm calling because of what's changing at {company} right now and how it affects supplier invoices. Have you got a minute?",
            ["opening_follow_up"] = "Hi {first}, {sdr} from Quillbridge. We were in touch before ({lastTouch}), so I wanted to follow up personally. Is now OK for a minute?",
            ["opening_referral"] = "Hi {first}, {sdr} from Quillbridge. {referrer} suggested I give you a call about supplier invoices. Have you got a minute?",
            ["opening_event"] = "Hi {first}, {sdr} from Quillbridge. You came across us before ({source}), so I wanted to put a voice to the name. Have you got a minute?",
            ["why_NewSystem"] = "{company} is changing accounting software: the best moment to fix how supplier invoices flow into it, instead of rebuilding the old manual steps.",
            ["why_NewLeader"] = "A new head of finance usually reviews processes in the first months, and supplier invoices are often one of the first things they look at.",
            ["why_Funding"] = "After new investment, growth usually means more supplier invoices without more people to handle them.",
            ["why_Hiring"] = "{company} is hiring invoice staff; worth checking whether part of that work can be automated first.",
            ["why_Audit"] = "With an outside audit coming, a clear record of who approved and paid each invoice matters.",
            ["why_Acquisition"] = "Bringing another company in usually means two ways of handling invoices that need to become one.",
            ["why_Expansion"] = "New sites or countries mean more suppliers and more invoices, often in new formats and currencies.",
            ["why_None"] = "Many {industry} companies the size of {company} still handle supplier invoices by hand; I'd like to understand how you do it.",
            ["why_role_FinanceLeader"] = "For you it is mostly about cost, control and visibility.",
            ["why_role_FinanceOperations"] = "For your team it is mostly about time spent per invoice and chasing approvals.",
            ["why_role_It"] = "For IT the key question is how it fits your systems.",
            ["why_role_Other"] = "I'd like to understand how your team is involved.",
            ["why_role_NonBuyer"] = "I'd like to find the right person in finance to talk to.",
            ["q_leader_1"] = "How much visibility do you have today on invoices waiting for approval and what you owe suppliers?",
            ["q_leader_2"] = "What would need to change for invoice handling to keep up with growth without adding people?",
            ["q_ops_1"] = "You handle invoices with {process} today: where does it slow down most?",
            ["q_ops_1_unknown"] = "How do supplier invoices reach your team today, and where do they get stuck?",
            ["q_ops_2"] = "How long does it usually take from receiving an invoice to paying it?",
            ["q_it_1"] = "How do supplier invoices get into {system} today?",
            ["q_it_1_other"] = "How do supplier invoices get into your accounting system today, and who maintains that?",
            ["q_it_1_unknown"] = "Which accounting system do you run, and how do invoices get into it?",
            ["q_it_2"] = "What do you need to check before finance can bring in a new tool?",
            ["q_other_1"] = "How does {company} handle supplier invoices today?",
            ["q_other_2"] = "Who owns that process, and how are you involved?",
            ["q_nonbuyer_1"] = "Who looks after paying supplier invoices at {company}?",
            ["q_nonbuyer_2"] = "What's the best way to reach them: would you mind introducing me?",
            ["core"] = "Quillbridge reads supplier invoices, sends them for approval, pays them and records them in your accounting software.",
            ["integration_supported"] = "It connects directly to {system}.",
            ["integration_not_supported"] = "On {system}: let me get our technical team to answer that.",
            ["integration_unknown"] = "Ask which accounting software they use before saying anything about connections.",
            ["story_hartwell"] = "Hartwell Logistics (UK) cut the time to process an invoice from 9 days to 2.",
            ["story_brennor"] = "Brennor Foods (Netherlands) handled 40% more invoices with no new hires.",
            ["story_callisto"] = "Callisto Health Group (US): its first audit after switching found no problems in how it pays bills.",
            ["ask"] = "Would you be open to a 30-minute call with one of our senior colleagues to see whether this fits {company}? What does your diary look like next week?",
            ["ask_referral"] = "Could you put me in touch with that person, so we can set up a 30-minute call with one of our senior colleagues?",
            ["pb_already_have_tool_q"] = "We already have a tool.",
            ["pb_already_have_tool_a"] = "Makes sense. What would you change about it if you could? If the answer is nothing, I won't take more of your time.",
            ["pb_no_budget_q"] = "No budget until next year.",
            ["pb_no_budget_a"] = "Understood, and I'm not asking for a decision now. A 30-minute call now helps you plan next year's budget with real numbers.",
            ["pb_send_email_q"] = "Just send me an email.",
            ["pb_send_email_a"] = "Happy to. So I send something useful: what's the biggest headache with supplier invoices today?",
            ["pb_switching_accounting_q"] = "We're switching our accounting software.",
            ["pb_switching_accounting_a"] = "That's often the best time to look at invoices, so the new system starts clean. Which system are you moving to?",
            ["pb_switching_accounting_a_supported"] = "Good timing: we connect directly to {switching}, so invoices can flow in from day one.",
            ["pb_switching_accounting_a_not_supported"] = "On {switching}: let me get our technical team to answer that. The move itself is still a good moment to review invoices.",
            ["pb_how_got_number_q"] = "How did you get my number?",
            ["pb_how_got_number_a"] = "Fair question. Your details came from {source}. If you'd rather not be called, tell me and I'll make sure we don't.",
            ["pb_not_interested_q"] = "We're not interested.",
            ["pb_not_interested_a"] = "Thanks for being direct. Can I ask one thing: is that because invoices work well today, or because the timing is wrong?",
            ["pb_integration_q"] = "Does it work with our accounting software?",
            ["pb_integration_a_supported"] = "Yes, we connect directly to {system}.",
            ["pb_integration_a_not_supported"] = "For {system}: let me get our technical team to answer that.",
            ["pb_integration_a_unknown"] = "Which one do you use? We connect to NetSuite, Sage Intacct and Microsoft Dynamics 365 Business Central; for anything else, let me get our technical team to answer that.",
            ["pb_pricing_q"] = "What does it cost?",
            ["pb_pricing_a"] = "It depends on how many invoices you handle; our senior colleague covers pricing properly in the 30-minute call.",
            ["pb_outside_accountant_q"] = "Our outside accountant handles the invoices.",
            ["pb_outside_accountant_a"] = "Good to know. Who approves the invoices before the accountant pays them? That step is often where the time goes.",
            ["pb_not_my_area_q"] = "That's not my area.",
            ["pb_not_my_area_a"] = "No problem. Who looks after supplier invoices at {company}, and would you mind introducing me?",
            ["plan_switchboard"] = "Ask for {first} {last}, {title}. If asked what it's about: \"supplier invoices, for the finance team\".",
            ["plan_voicemail"] = "\"Hi {first}, {sdr} from Quillbridge, about how {company} handles supplier invoices. I'll send a short email and try you again.\"",
            ["plan_email"] = "Agree, then ask one question so the email is useful, and propose a 30-minute call in it.",
            ["plan_gatekeeper"] = "Be honest: \"It's a sales call about supplier invoices. When is a good time to reach {first}?\"",
            ["plan_wrong"] = "Ask who handles supplier invoices at {company} and whether they'd introduce you; thank them either way.",
            ["last_touch"] = "Last contact: {lastTouch}. Mention it, and keep any promise made then."
        },
        ["de"] = new()
        {
            ["their_role"] = "die zuständige Person für Lieferantenrechnungen",
            ["a_contact"] = "einer unserer Kontakte",
            ["opening_cold"] = "Hallo {first}, hier ist {sdr} von Quillbridge. Wir helfen Finanzteams bei Lieferantenrechnungen. Passt eine kurze Frage gerade gar nicht?",
            ["opening_trigger"] = "Hallo {first}, {sdr} von Quillbridge. Ich rufe an, weil sich bei {company} gerade etwas ändert, das auch die Lieferantenrechnungen betrifft. Haben Sie eine Minute?",
            ["opening_follow_up"] = "Hallo {first}, {sdr} von Quillbridge. Wir hatten schon Kontakt ({lastTouch}), daher wollte ich mich persönlich melden. Passt es kurz?",
            ["opening_referral"] = "Hallo {first}, {sdr} von Quillbridge. {referrer} hat mir empfohlen, Sie wegen Lieferantenrechnungen anzurufen. Haben Sie eine Minute?",
            ["opening_event"] = "Hallo {first}, {sdr} von Quillbridge. Sie kennen uns bereits ({source}), daher wollte ich mich einmal persönlich melden. Haben Sie eine Minute?",
            ["why_NewSystem"] = "{company} stellt die Buchhaltungssoftware um: der beste Moment, den Rechnungsfluss neu aufzusetzen, statt alte manuelle Schritte nachzubauen.",
            ["why_NewLeader"] = "Eine neue Finanzleitung prüft in den ersten Monaten meist die Abläufe, und Lieferantenrechnungen stehen oft weit oben.",
            ["why_Funding"] = "Nach einer Finanzierungsrunde heißt Wachstum meist: mehr Lieferantenrechnungen, ohne mehr Leute dafür.",
            ["why_Hiring"] = "{company} stellt Personal für Rechnungen ein; es lohnt sich zu prüfen, ob ein Teil der Arbeit vorher automatisiert werden kann.",
            ["why_Audit"] = "Vor einer externen Prüfung zählt ein sauberer Nachweis, wer jede Rechnung freigegeben und bezahlt hat.",
            ["why_Acquisition"] = "Eine Übernahme bedeutet meist zwei Arten, Rechnungen zu bearbeiten, die zusammengeführt werden müssen.",
            ["why_Expansion"] = "Neue Standorte oder Länder bringen mehr Lieferanten und Rechnungen, oft in neuen Formaten und Währungen.",
            ["why_None"] = "Viele Unternehmen der Branche {industry} in der Größe von {company} bearbeiten Lieferantenrechnungen noch von Hand; ich möchte verstehen, wie Sie es machen.",
            ["why_role_FinanceLeader"] = "Für Sie geht es vor allem um Kosten, Kontrolle und Transparenz.",
            ["why_role_FinanceOperations"] = "Für Ihr Team geht es vor allem um Zeit pro Rechnung und das Nachhalten von Freigaben.",
            ["why_role_It"] = "Für die IT ist die Kernfrage, wie es zu Ihren Systemen passt.",
            ["why_role_Other"] = "Ich möchte verstehen, wie Ihr Team beteiligt ist.",
            ["why_role_NonBuyer"] = "Ich suche die richtige Ansprechperson im Finanzbereich.",
            ["q_leader_1"] = "Wie gut sehen Sie heute, welche Rechnungen auf Freigabe warten und was Sie Lieferanten schulden?",
            ["q_leader_2"] = "Was müsste sich ändern, damit die Rechnungsbearbeitung mit dem Wachstum mithält, ohne mehr Personal?",
            ["q_ops_1"] = "Sie arbeiten heute mit {process}: wo hakt es am meisten?",
            ["q_ops_1_unknown"] = "Wie kommen Lieferantenrechnungen heute zu Ihrem Team, und wo bleiben sie hängen?",
            ["q_ops_2"] = "Wie lange dauert es meist vom Rechnungseingang bis zur Zahlung?",
            ["q_it_1"] = "Wie kommen Lieferantenrechnungen heute in {system}?",
            ["q_it_1_other"] = "Wie kommen Lieferantenrechnungen heute in Ihre Buchhaltungssoftware, und wer betreut das?",
            ["q_it_1_unknown"] = "Welche Buchhaltungssoftware nutzen Sie, und wie kommen die Rechnungen hinein?",
            ["q_it_2"] = "Was müssen Sie prüfen, bevor die Finanzabteilung ein neues Tool einführen kann?",
            ["q_other_1"] = "Wie bearbeitet {company} heute Lieferantenrechnungen?",
            ["q_other_2"] = "Wer verantwortet den Prozess, und wie sind Sie beteiligt?",
            ["q_nonbuyer_1"] = "Wer kümmert sich bei {company} um die Bezahlung von Lieferantenrechnungen?",
            ["q_nonbuyer_2"] = "Wie erreiche ich diese Person am besten: würden Sie mich vorstellen?",
            ["core"] = "Quillbridge liest Lieferantenrechnungen ein, schickt sie zur Freigabe, bezahlt sie und verbucht sie in Ihrer Buchhaltungssoftware.",
            ["integration_supported"] = "Es ist direkt mit {system} verbunden.",
            ["integration_not_supported"] = "Zu {system}: das lasse ich gern unser technisches Team beantworten.",
            ["integration_unknown"] = "Erst nach der Buchhaltungssoftware fragen, bevor Sie etwas zu Anbindungen sagen.",
            ["story_hartwell"] = "Hartwell Logistics (UK) hat die Bearbeitungszeit pro Rechnung von 9 auf 2 Tage gesenkt.",
            ["story_brennor"] = "Brennor Foods (Niederlande) bearbeitet 40 % mehr Rechnungen ohne zusätzliche Mitarbeitende.",
            ["story_callisto"] = "Callisto Health Group (USA): Die erste Prüfung nach der Umstellung fand keine Probleme bei der Bezahlung der Rechnungen.",
            ["ask"] = "Wären Sie offen für ein 30-minütiges Gespräch mit einer erfahrenen Kollegin oder einem Kollegen, um zu sehen, ob das zu {company} passt? Wie sieht Ihr Kalender nächste Woche aus?",
            ["ask_referral"] = "Könnten Sie mich mit dieser Person verbinden, damit wir ein 30-minütiges Gespräch mit einem erfahrenen Kollegen vereinbaren?",
            ["pb_already_have_tool_q"] = "Wir haben schon ein Tool.",
            ["pb_already_have_tool_a"] = "Verstehe. Was würden Sie daran ändern, wenn Sie könnten? Wenn nichts, halte ich Sie nicht weiter auf.",
            ["pb_no_budget_q"] = "Kein Budget bis nächstes Jahr.",
            ["pb_no_budget_a"] = "Verstanden, ich frage nicht nach einer Entscheidung. Ein 30-minütiges Gespräch jetzt hilft, das Budget fürs nächste Jahr mit echten Zahlen zu planen.",
            ["pb_send_email_q"] = "Schicken Sie mir einfach eine E-Mail.",
            ["pb_send_email_a"] = "Gern. Damit sie nützlich ist: Was ist heute das größte Problem bei Lieferantenrechnungen?",
            ["pb_switching_accounting_q"] = "Wir wechseln gerade die Buchhaltungssoftware.",
            ["pb_switching_accounting_a"] = "Das ist oft der beste Zeitpunkt, die Rechnungen gleich sauber aufzusetzen. Zu welchem System wechseln Sie?",
            ["pb_switching_accounting_a_supported"] = "Gutes Timing: Wir sind direkt mit {switching} verbunden, die Rechnungen fließen also ab dem ersten Tag hinein.",
            ["pb_switching_accounting_a_not_supported"] = "Zu {switching}: das lasse ich gern unser technisches Team beantworten. Der Wechsel ist trotzdem ein guter Moment, die Rechnungen anzuschauen.",
            ["pb_how_got_number_q"] = "Woher haben Sie meine Nummer?",
            ["pb_how_got_number_a"] = "Berechtigte Frage. Ihre Daten stammen aus: {source}. Wenn Sie keine Anrufe möchten, sagen Sie es, und wir halten uns daran.",
            ["pb_not_interested_q"] = "Kein Interesse.",
            ["pb_not_interested_a"] = "Danke für die Offenheit. Eine Frage noch: Läuft es mit den Rechnungen gut, oder passt nur der Zeitpunkt nicht?",
            ["pb_integration_q"] = "Funktioniert das mit unserer Buchhaltungssoftware?",
            ["pb_integration_a_supported"] = "Ja, wir sind direkt mit {system} verbunden.",
            ["pb_integration_a_not_supported"] = "Zu {system}: das lasse ich gern unser technisches Team beantworten.",
            ["pb_integration_a_unknown"] = "Welche nutzen Sie? Wir sind mit NetSuite, Sage Intacct und Microsoft Dynamics 365 Business Central verbunden; bei allem anderen: das lasse ich gern unser technisches Team beantworten.",
            ["pb_pricing_q"] = "Was kostet das?",
            ["pb_pricing_a"] = "Das hängt von der Zahl Ihrer Rechnungen ab; die Preise bespricht unser erfahrener Kollege im 30-minütigen Gespräch.",
            ["pb_outside_accountant_q"] = "Das macht unser Steuerberater.",
            ["pb_outside_accountant_a"] = "Gut zu wissen. Wer gibt die Rechnungen frei, bevor sie bezahlt werden? Dort geht oft die meiste Zeit verloren.",
            ["pb_not_my_area_q"] = "Dafür bin ich nicht zuständig.",
            ["pb_not_my_area_a"] = "Kein Problem. Wer kümmert sich bei {company} um Lieferantenrechnungen, und würden Sie mich vorstellen?",
            ["plan_switchboard"] = "Nach {first} {last} fragen ({title}). Auf die Frage, worum es geht: \"Lieferantenrechnungen, für die Finanzabteilung\".",
            ["plan_voicemail"] = "\"Hallo {first}, {sdr} von Quillbridge, es geht um Lieferantenrechnungen bei {company}. Ich schicke eine kurze E-Mail und versuche es noch einmal.\"",
            ["plan_email"] = "Zusagen, dann eine Frage stellen, damit die E-Mail passt, und darin ein 30-minütiges Gespräch vorschlagen.",
            ["plan_gatekeeper"] = "Ehrlich sein: \"Es ist ein Vertriebsanruf zu Lieferantenrechnungen. Wann erreiche ich {first} am besten?\"",
            ["plan_wrong"] = "Fragen, wer bei {company} für Lieferantenrechnungen zuständig ist und ob die Person Sie vorstellt; in jedem Fall bedanken.",
            ["last_touch"] = "Letzter Kontakt: {lastTouch}. Darauf Bezug nehmen und Zusagen von damals einhalten."
        },
        ["it"] = new()
        {
            ["their_role"] = "la persona responsabile delle fatture fornitori",
            ["a_contact"] = "uno dei nostri contatti",
            ["opening_cold"] = "Buongiorno {first}, sono {sdr} di Quillbridge. Aiutiamo i team finanziari a gestire le fatture dei fornitori. È un brutto momento per una domanda veloce?",
            ["opening_trigger"] = "Buongiorno {first}, {sdr} di Quillbridge. La chiamo per quello che sta cambiando in {company} e per come tocca le fatture fornitori. Ha un minuto?",
            ["opening_follow_up"] = "Buongiorno {first}, {sdr} di Quillbridge. Ci siamo già sentiti ({lastTouch}), quindi volevo richiamarla di persona. Ha un minuto?",
            ["opening_referral"] = "Buongiorno {first}, {sdr} di Quillbridge. {referrer} mi ha suggerito di chiamarla per le fatture fornitori. Ha un minuto?",
            ["opening_event"] = "Buongiorno {first}, {sdr} di Quillbridge. Ci conosce già ({source}), quindi volevo presentarmi di persona. Ha un minuto?",
            ["why_NewSystem"] = "{company} sta cambiando software contabile: il momento migliore per sistemare il flusso delle fatture, invece di ricostruire i vecchi passaggi manuali.",
            ["why_NewLeader"] = "Un nuovo responsabile finanziario di solito rivede i processi nei primi mesi, e le fatture fornitori sono spesso tra i primi punti.",
            ["why_Funding"] = "Dopo un nuovo investimento, crescere di solito significa più fatture fornitori senza più persone per gestirle.",
            ["why_Hiring"] = "{company} sta assumendo personale per le fatture; vale la pena capire se parte del lavoro si può automatizzare prima.",
            ["why_Audit"] = "Con una revisione esterna in arrivo, conta avere traccia chiara di chi ha approvato e pagato ogni fattura.",
            ["why_Acquisition"] = "Un'acquisizione di solito significa due modi di gestire le fatture da unificare.",
            ["why_Expansion"] = "Nuove sedi o nuovi paesi portano più fornitori e più fatture, spesso in formati e valute nuovi.",
            ["why_None"] = "Molte aziende del settore {industry} delle dimensioni di {company} gestiscono ancora le fatture fornitori a mano; vorrei capire come lavorate voi.",
            ["why_role_FinanceLeader"] = "Per lei si tratta soprattutto di costi, controllo e visibilità.",
            ["why_role_FinanceOperations"] = "Per il suo team si tratta soprattutto del tempo per fattura e del rincorrere le approvazioni.",
            ["why_role_It"] = "Per l'IT la domanda chiave è come si integra con i vostri sistemi.",
            ["why_role_Other"] = "Vorrei capire come è coinvolto il suo team.",
            ["why_role_NonBuyer"] = "Vorrei trovare la persona giusta in amministrazione con cui parlare.",
            ["q_leader_1"] = "Quanta visibilità ha oggi sulle fatture in attesa di approvazione e su quanto dovete ai fornitori?",
            ["q_leader_2"] = "Cosa dovrebbe cambiare perché la gestione delle fatture regga la crescita senza aggiungere persone?",
            ["q_ops_1"] = "Oggi gestite le fatture con {process}: dove si rallenta di più?",
            ["q_ops_1_unknown"] = "Come arrivano oggi le fatture fornitori al suo team, e dove si bloccano?",
            ["q_ops_2"] = "Quanto tempo passa di solito tra la ricezione di una fattura e il pagamento?",
            ["q_it_1"] = "Come entrano oggi le fatture fornitori in {system}?",
            ["q_it_1_other"] = "Come entrano oggi le fatture fornitori nel vostro software contabile, e chi se ne occupa?",
            ["q_it_1_unknown"] = "Quale software contabile usate, e come ci entrano le fatture?",
            ["q_it_2"] = "Cosa dovete verificare prima che l'amministrazione possa adottare un nuovo strumento?",
            ["q_other_1"] = "Come gestisce oggi {company} le fatture fornitori?",
            ["q_other_2"] = "Chi è responsabile del processo, e lei come è coinvolto?",
            ["q_nonbuyer_1"] = "Chi si occupa del pagamento delle fatture fornitori in {company}?",
            ["q_nonbuyer_2"] = "Qual è il modo migliore per contattare questa persona: potrebbe presentarmi?",
            ["core"] = "Quillbridge legge le fatture fornitori, le manda in approvazione, le paga e le registra nel vostro software contabile.",
            ["integration_supported"] = "Si collega direttamente a {system}.",
            ["integration_not_supported"] = "Su {system}: lascio rispondere il nostro team tecnico.",
            ["integration_unknown"] = "Chiedere quale software contabile usano prima di dire qualsiasi cosa sui collegamenti.",
            ["story_hartwell"] = "Hartwell Logistics (Regno Unito) ha ridotto il tempo di lavorazione di una fattura da 9 a 2 giorni.",
            ["story_brennor"] = "Brennor Foods (Paesi Bassi) gestisce il 40% di fatture in più senza nuove assunzioni.",
            ["story_callisto"] = "Callisto Health Group (USA): il primo audit dopo il passaggio non ha trovato problemi nel modo in cui paga i fornitori.",
            ["ask"] = "Sarebbe disponibile per una chiamata di 30 minuti con un nostro collega senior, per capire se fa al caso di {company}? Come è messa la sua agenda la prossima settimana?",
            ["ask_referral"] = "Potrebbe mettermi in contatto con questa persona, per fissare una chiamata di 30 minuti con un nostro collega senior?",
            ["pb_already_have_tool_q"] = "Abbiamo già uno strumento.",
            ["pb_already_have_tool_a"] = "Ha senso. Cosa cambierebbe, se potesse? Se la risposta è niente, non le rubo altro tempo.",
            ["pb_no_budget_q"] = "Niente budget fino al prossimo anno.",
            ["pb_no_budget_a"] = "Capisco, non le chiedo una decisione ora. Una chiamata di 30 minuti adesso aiuta a pianificare il budget del prossimo anno con numeri reali.",
            ["pb_send_email_q"] = "Mi mandi una email.",
            ["pb_send_email_a"] = "Volentieri. Perché sia utile: qual è oggi il problema principale con le fatture fornitori?",
            ["pb_switching_accounting_q"] = "Stiamo cambiando software contabile.",
            ["pb_switching_accounting_a"] = "Spesso è il momento migliore per sistemare le fatture, così il nuovo sistema parte pulito. A quale sistema passate?",
            ["pb_switching_accounting_a_supported"] = "Ottimo tempismo: ci colleghiamo direttamente a {switching}, quindi le fatture entrano dal primo giorno.",
            ["pb_switching_accounting_a_not_supported"] = "Su {switching}: lascio rispondere il nostro team tecnico. Il passaggio resta comunque un buon momento per rivedere le fatture.",
            ["pb_how_got_number_q"] = "Come ha avuto il mio numero?",
            ["pb_how_got_number_a"] = "Domanda giusta. I suoi dati vengono da: {source}. Se preferisce non essere chiamato, me lo dica e lo rispetteremo.",
            ["pb_not_interested_q"] = "Non ci interessa.",
            ["pb_not_interested_a"] = "Grazie della franchezza. Posso chiederle una cosa: le fatture funzionano bene oggi, o è solo il momento sbagliato?",
            ["pb_integration_q"] = "Funziona con il nostro software contabile?",
            ["pb_integration_a_supported"] = "Sì, ci colleghiamo direttamente a {system}.",
            ["pb_integration_a_not_supported"] = "Per {system}: lascio rispondere il nostro team tecnico.",
            ["pb_integration_a_unknown"] = "Quale usate? Ci colleghiamo a NetSuite, Sage Intacct e Microsoft Dynamics 365 Business Central; per tutto il resto lascio rispondere il nostro team tecnico.",
            ["pb_pricing_q"] = "Quanto costa?",
            ["pb_pricing_a"] = "Dipende da quante fatture gestite; i prezzi li spiega bene il nostro collega senior nella chiamata di 30 minuti.",
            ["pb_outside_accountant_q"] = "Le fatture le gestisce il nostro commercialista.",
            ["pb_outside_accountant_a"] = "Buono a sapersi. Chi approva le fatture prima che vengano pagate? È spesso lì che si perde più tempo.",
            ["pb_not_my_area_q"] = "Non è il mio ambito.",
            ["pb_not_my_area_a"] = "Nessun problema. Chi si occupa delle fatture fornitori in {company}, e potrebbe presentarmi?",
            ["plan_switchboard"] = "Chiedere di {first} {last} ({title}). Se chiedono il motivo: \"fatture fornitori, per l'amministrazione\".",
            ["plan_voicemail"] = "\"Buongiorno {first}, sono {sdr} di Quillbridge, riguardo alle fatture fornitori di {company}. Le mando una breve email e la richiamo.\"",
            ["plan_email"] = "Accettare, poi fare una domanda perché l'email sia utile, e proporre lì una chiamata di 30 minuti.",
            ["plan_gatekeeper"] = "Essere onesti: \"È una chiamata commerciale sulle fatture fornitori. Quando posso trovare {first}?\"",
            ["plan_wrong"] = "Chiedere chi si occupa delle fatture fornitori in {company} e se può presentarvi; ringraziare comunque.",
            ["last_touch"] = "Ultimo contatto: {lastTouch}. Citarlo e mantenere le promesse fatte allora."
        }
    };
}
