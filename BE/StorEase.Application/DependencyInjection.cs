using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using StorEase.Application.Features.AccessCodes;
using StorEase.Application.Features.Auth;
using StorEase.Application.Features.Contracts;
using StorEase.Application.Features.Facilities;
using StorEase.Application.Features.Handovers;
using StorEase.Application.Features.MoveOuts;
using StorEase.Application.Features.Renewals;
using StorEase.Application.Features.Maintenance;
using StorEase.Application.Features.Reservations;
using StorEase.Application.Features.Invoices;
using StorEase.Application.Features.Pricing;
using StorEase.Application.Features.Staff;
using StorEase.Application.Features.StorageUnits;
using StorEase.Application.Features.SupportTickets;

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
        services.AddScoped<IMoveOutService, MoveOutService>();
        services.AddScoped<IRenewalService, RenewalService>();
        services.AddScoped<ITicketQueueService, TicketQueueService>();
        services.AddScoped<ISupportTicketService, SupportTicketService>();
        services.AddScoped<IStaffAccountService, StaffAccountService>();
        services.AddScoped<IMaintenanceService, MaintenanceService>();
        services.AddScoped<IStaffTaskService, StaffTaskService>();
        services.AddScoped<IShiftService, ShiftService>();
        return services;
    }
}
