using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using StorEase.Application.Features.AccessCodes;
using StorEase.Application.Features.ActivityLogs;
using StorEase.Application.Features.Auth;
using StorEase.Application.Features.Billing;
using StorEase.Application.Features.Contracts;
using StorEase.Application.Features.Facilities;
using StorEase.Application.Features.Handovers;
using StorEase.Application.Features.Returns;
using StorEase.Application.Features.Renewals;
using StorEase.Application.Features.Maintenance;
using StorEase.Application.Features.Reservations;
using StorEase.Application.Features.Invoices;
using StorEase.Application.Features.OverdueCases;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Features.Reports;
using StorEase.Application.Features.Staff;
using StorEase.Application.Features.StorageUnits;
using StorEase.Application.Features.SupportTickets;
using StorEase.Application.Features.UnitVisits;
using StorEase.Application.Features.DepositRefunds;
using StorEase.Application.Features.Users;

namespace StorEase.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IFacilityService, FacilityService>();
        services.AddScoped<IStorageUnitService, StorageUnitService>();
        services.AddScoped<IUnitCatalogService, UnitCatalogService>();
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IReservationService, ReservationService>();
        services.AddScoped<IHandoverService, HandoverService>();
        services.AddScoped<IContractService, ContractService>();
        services.AddScoped<IAccessCodeService, AccessCodeService>();
        services.AddScoped<IAccessLogService, AccessLogService>();
        services.AddScoped<IReturnService, ReturnService>();
        services.AddScoped<IRenewalService, RenewalService>();
        services.AddScoped<ITicketQueueService, TicketQueueService>();
        services.AddScoped<ISupportTicketService, SupportTicketService>();
        services.AddScoped<IStaffAccountService, StaffAccountService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddScoped<IStaffTaskService, StaffTaskService>();
        services.AddScoped<IShiftService, ShiftService>();
        services.AddScoped<IActivityLogService, ActivityLogService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IFloorService, FloorService>();
        services.AddScoped<IHandoverEvidenceService, HandoverEvidenceService>();
        services.AddScoped<IReminderService, ReminderService>();
        services.AddScoped<IBillingCycleService, BillingCycleService>();
        services.AddScoped<IOverdueService, OverdueService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IUnitVisitService, UnitVisitService>();
        services.AddScoped<IDepositRefundService, DepositRefundService>();
        services.AddScoped<IWalkInService, WalkInService>();
        return services;
    }
}
