using System.Text.RegularExpressions;
using quillbridge.Domain.Enums;

namespace quillbridge.Application.BusinessRules;

// places a job title (English, German, Italian, French, Dutch, Spanish, Portuguese) into one of the roles
// the brief names. Order matters: "Head of Finance Operations" is operations, not a finance leader.
public static partial class RoleDetector
{
    [GeneratedRegex(@"office manager|sales|marketing|executive assistant|assistant|operations coordinator|\bhr\b|human resources|product manager|partnership|reception", RegexOptions.IgnoreCase)]
    private static partial Regex NonBuyerPattern();

    [GeneratedRegex(@"\bit\b|\bit-|\bict\b|\bdsi\b|\bcio\b|\bcto\b|information technology|sistemas|\bti\b|systems manager|informatica|informatique", RegexOptions.IgnoreCase)]
    private static partial Regex ItPattern();

    // "AP" is the team that pays supplier bills
    [GeneratedRegex(@"accounts payable|\bap\b|kreditoren|crediteuren|fornitori|fournisseurs|cuentas a pagar|contas a pagar|controller|contr[oô]le|finance operations|finance manager|accounting|buchhaltung|contabili|comptab|financi[eë]le administratie|amministrativo", RegexOptions.IgnoreCase)]
    private static partial Regex FinanceOperationsPattern();

    [GeneratedRegex(@"\bcfo\b|chief financial|finance director|director of finance|head of finance|vp,? finance|vice president,? finance|financieel directeur|direttore finanziario|director financiero|directora financiera|diretor financeiro|\bdaf\b|leitung finanzen|finanzleiter|kaufm[äa]nnische|amministrazione e finanza|finanzchef", RegexOptions.IgnoreCase)]
    private static partial Regex FinanceLeaderPattern();

    // a bare "Finance" title is someone in the finance team
    [GeneratedRegex(@"financ|finanz|finanç", RegexOptions.IgnoreCase)]
    private static partial Regex FinanceTeamPattern();

    public static ProspectRole Detect(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return ProspectRole.NonBuyer;
        }

        if (NonBuyerPattern().IsMatch(title))
        {
            return ProspectRole.NonBuyer;
        }

        if (ItPattern().IsMatch(title))
        {
            return ProspectRole.It;
        }

        if (FinanceLeaderPattern().IsMatch(title) && !title.Contains("operations", StringComparison.OrdinalIgnoreCase))
        {
            return ProspectRole.FinanceLeader;
        }

        if (FinanceOperationsPattern().IsMatch(title) || FinanceTeamPattern().IsMatch(title))
        {
            return ProspectRole.FinanceOperations;
        }

        return ProspectRole.Other;
    }
}
