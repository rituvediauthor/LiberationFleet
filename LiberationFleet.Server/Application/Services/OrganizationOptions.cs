namespace LiberationFleet.Server.Application.Services;

/// <summary>
/// Legal / tax identity used in donor acknowledgments (not branding/FromName).
/// </summary>
public class OrganizationOptions
{
    public const string SectionName = "Organization";

    /// <summary>Legal entity name on acknowledgments, e.g. Liberation Fleet Co.</summary>
    public string LegalName { get; set; } = "Liberation Fleet Co.";

    /// <summary>Employer Identification Number (XX-XXXXXXX). Public for many nonprofits; set via config.</summary>
    public string Ein { get; set; } = string.Empty;

    /// <summary>Optional mailing address for the acknowledgment footer.</summary>
    public string MailingAddress { get; set; } = string.Empty;

    /// <summary>
    /// Short status line for the letter (federal/state exemption). Counsel should approve production copy.
    /// </summary>
    public string TaxExemptStatement { get; set; } =
        "Liberation Fleet Co. is a tax-exempt nonprofit corporation. Contributions may be tax-deductible to the extent permitted by applicable law.";
}
