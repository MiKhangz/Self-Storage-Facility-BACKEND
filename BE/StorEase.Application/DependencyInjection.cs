using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using StorEase.Application.Features.Auth;
using StorEase.Application.Features.Contracts;
using StorEase.Application.Features.Facilities;
using StorEase.Application.Features.Handovers;
using StorEase.Application.Features.Reservations;
using StorEase.Application.Features.StorageUnits;
using StorEase.Application.Features.Staff;
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
        services.AddScoped<IReservationService, ReservationService>();
        services.AddScoped<IHandoverService, HandoverService>();
        services.AddScoped<IContractService, ContractServiceNotReady>();
        services.AddScoped<ITicketQueueService, TicketQueueService>();
        services.AddScoped<ISupportTicketService, SupportTicketService>();
        services.AddScoped<IStaffAccountService, StaffAccountService>();
        return services;
    }
}
