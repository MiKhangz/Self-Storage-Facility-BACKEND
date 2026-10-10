using Microsoft.EntityFrameworkCore;
using StorEase.Domain.Entities;

namespace StorEase.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<StaffFacility> StaffFacilities => Set<StaffFacility>();
    public DbSet<Shift> Shifts => Set<Shift>();
    public DbSet<StaffTask> StaffTasks => Set<StaffTask>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<UnitType> UnitTypes => Set<UnitType>();
    public DbSet<StorageUnit> StorageUnits => Set<StorageUnit>();
    public DbSet<MaintenanceOrder> MaintenanceOrders => Set<MaintenanceOrder>();
    public DbSet<PricingPolicy> PricingPolicies => Set<PricingPolicy>();
    public DbSet<UnitPrice> UnitPrices => Set<UnitPrice>();
    public DbSet<FeeType> FeeTypes => Set<FeeType>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<Handover> Handovers => Set<Handover>();
    public DbSet<Contract> Contracts => Set<Contract>();
    public DbSet<AccessCode> AccessCodes => Set<AccessCode>();
    public DbSet<AccessLog> AccessLogs => Set<AccessLog>();
    public DbSet<Renewal> Renewals => Set<Renewal>();
    public DbSet<MoveOutRequest> MoveOutRequests => Set<MoveOutRequest>();
    public DbSet<ReturnInspection> ReturnInspections => Set<ReturnInspection>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<OverdueCase> OverdueCases => Set<OverdueCase>();
    public DbSet<Reminder> Reminders => Set<Reminder>();
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Nothing is hard-deleted, and Restrict also avoids SQL Server's multiple-cascade-path error.
        foreach (var fk in modelBuilder.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
