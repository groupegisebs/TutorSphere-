namespace TutorSphere.Application.Common;

public class PlatformBillingOptions
{
    public const string SectionName = "PlatformBilling";

    public decimal AnnualFeeCad { get; set; } = 10.00m;
    public string Currency { get; set; } = "USD";
    public string ProductCode { get; set; } = "TUTORSPHERE-LICENSE-ANNUAL";
    public string PlanCode { get; set; } = "YEARLY";
    public int RenewalReminderDays { get; set; } = 30;
}
