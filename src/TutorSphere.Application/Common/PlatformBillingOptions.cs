namespace TutorSphere.Application.Common;

public class PlatformBillingOptions
{
    public const string SectionName = "PlatformBilling";

    public decimal AnnualFeeCad { get; set; } = 25.00m;
    public string Currency { get; set; } = "CAD";
    public string ProductCode { get; set; } = "TUTORSPHERE-LICENSE-ANNUAL";
    public string PlanCode { get; set; } = "ANNUAL";
    public int RenewalReminderDays { get; set; } = 30;
}
