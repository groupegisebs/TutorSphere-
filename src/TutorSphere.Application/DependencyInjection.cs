using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TutorSphere.Application.Common.Interfaces;
using TutorSphere.Application.Options;
using TutorSphere.Application.Services;

namespace TutorSphere.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration? configuration = null)
    {
        if (configuration is not null)
            services.Configure<ExpertModuleFeatureOptions>(configuration.GetSection(ExpertModuleFeatureOptions.SectionName));
        else
            services.Configure<ExpertModuleFeatureOptions>(_ => { });

        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<IBrandingService, BrandingService>();
        services.AddScoped<ISearchService, SearchService>();
        services.AddScoped<IHomeworkService, HomeworkService>();
        services.AddScoped<ILessonReportService, LessonReportService>();
        services.AddScoped<ILessonService, LessonService>();
        services.AddScoped<ILessonAccessService, LessonAccessService>();
        services.AddScoped<ICalendarService, CalendarService>();
        services.AddScoped<IStudentService, StudentService>();
        services.AddScoped<IStudentPortalService, StudentPortalService>();
        services.AddScoped<IParentService, ParentService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<ISubscriptionOfferingService, SubscriptionOfferingService>();
        services.AddScoped<IStudentSubscriptionService, StudentSubscriptionService>();
        services.AddScoped<ISubscriptionLessonScheduler, SubscriptionLessonScheduler>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<ITutorEarningsService, TutorEarningsService>();
        services.AddScoped<ITutorPayoutAccountService, TutorPayoutAccountService>();
        services.AddScoped<IBillingEmailOrchestrator, BillingEmailOrchestrator>();
        services.AddScoped<IPlatformBillingService, PlatformBillingService>();
        services.AddScoped<IPlatformPromoService, PlatformPromoService>();
        services.AddScoped<ITeacherLicenseActivationService, TeacherLicenseActivationService>();
        services.AddScoped<ITutorOnboardingService, TutorOnboardingService>();
        services.AddScoped<IExpertGroupService, ExpertGroupService>();
        services.AddScoped<IExpertGroupManagerService, ExpertGroupManagerService>();
        services.AddScoped<IGroupOfferService, GroupOfferService>();
        services.AddScoped<IGroupAdminChatService, GroupAdminChatService>();
        services.AddScoped<ITeacherInterestService, TeacherInterestService>();
        services.AddScoped<IExpertDelegatedTaskService, ExpertDelegatedTaskService>();
        services.AddScoped<IExpertModuleFeatureService, ExpertModuleFeatureService>();
        services.AddScoped<IExpertApprovalService, ExpertApprovalService>();
        services.AddScoped<IExpertMonitoringService, ExpertMonitoringService>();
        services.AddScoped<IExpertDisciplineService, ExpertDisciplineService>();
        services.AddScoped<IExpertReviewNotificationService, ExpertReviewNotificationService>();
        services.AddScoped<IExpertMembershipGovernanceService, ExpertMembershipGovernanceService>();
        services.AddScoped<IExpertGroupMemberAdminService, ExpertGroupMemberAdminService>();
        services.AddScoped<IExpertMeetingService, ExpertMeetingService>();
        services.AddScoped<IExpertDashboardService, ExpertDashboardService>();
        services.AddScoped<IExpertGovernanceAuditService, ExpertGovernanceAuditService>();
        services.AddScoped<IExpertWorkspaceService, ExpertWorkspaceService>();
        services.AddScoped<ITeacherDocumentService, TeacherDocumentService>();
        services.AddScoped<ITeacherSchoolAdminService, TeacherSchoolAdminService>();
        services.AddScoped<IGroupAdminAccessService, GroupAdminAccessService>();
        services.AddScoped<IParentEngagementService, ParentEngagementService>();
        services.AddScoped<IParentMailboxService, ParentMailboxService>();
        return services;
    }
}
