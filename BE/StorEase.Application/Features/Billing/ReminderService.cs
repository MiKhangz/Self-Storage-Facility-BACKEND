using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.Billing;

public static class ReminderTemplates
{
    public const string DueSoon = "DueSoon";
    public const string Overdue = "Overdue";
    public const string Manual = "Manual";
}

public sealed record ReminderDto(int ReminderId, int InvoiceId, ReminderChannel Channel, string Template, DateTime? SentAt, ReminderStatus Status);

public interface IReminderService
{
    Task<Result<IReadOnlyList<ReminderDto>>> ListAsync(int invoiceId, CancellationToken ct);

    /// <summary>Staff send a reminder now, regardless of the customer's preference (it is about money owed).</summary>
    Task<Result<ReminderDto>> SendNowAsync(int invoiceId, CancellationToken ct);

    /// <summary>
    /// Emails the invoice's customer and records the attempt. Returns null when nothing was sent: the customer
    /// opted out, or this template already went out today.
    /// </summary>
    Task<ReminderStatus?> SendAsync(int invoiceId, string template, bool onlyIfOptedIn, CancellationToken ct);
}

public sealed class ReminderService(IUnitOfWork uow, ICurrentUser currentUser, IEmailService email) : IReminderService
{
    public async Task<Result<IReadOnlyList<ReminderDto>>> ListAsync(int invoiceId, CancellationToken ct)
    {
        if (!await CanSeeAsync(invoiceId, ct))
            return Error.NotFound("Invoice not found.");
        return await uow.Repository<Reminder>().Query().AsNoTracking()
            .Where(r => r.InvoiceId == invoiceId)
            .OrderByDescending(r => r.ReminderId)
            .Select(r => new ReminderDto(r.ReminderId, r.InvoiceId, r.Channel, r.Template, r.SentAt, r.Status))
            .ToListAsync(ct);
    }

    public async Task<Result<ReminderDto>> SendNowAsync(int invoiceId, CancellationToken ct)
    {
        if (!await CanSeeAsync(invoiceId, ct))
            return Error.NotFound("Invoice not found.");
        var invoice = await uow.Repository<Invoice>().FindAsync(invoiceId, ct);
        if (invoice!.Status is InvoiceStatus.Paid or InvoiceStatus.WrittenOff)
            return Error.Conflict("This invoice is already closed.");
        if (await SendAsync(invoiceId, ReminderTemplates.Manual, onlyIfOptedIn: false, ct) is null)
            return Error.Conflict("A reminder for this invoice was already sent today.");

        var reminder = await uow.Repository<Reminder>().Query().AsNoTracking()
            .Where(r => r.InvoiceId == invoiceId).OrderByDescending(r => r.ReminderId).FirstAsync(ct);
        return new ReminderDto(reminder.ReminderId, reminder.InvoiceId, reminder.Channel, reminder.Template, reminder.SentAt, reminder.Status);
    }

    public async Task<ReminderStatus?> SendAsync(int invoiceId, string template, bool onlyIfOptedIn, CancellationToken ct)
    {
        var invoice = await uow.Repository<Invoice>().Query().AsNoTracking()
            .Where(i => i.InvoiceId == invoiceId)
            .Select(i => new { i.InvoiceNumber, i.DueDate, Balance = i.TotalAmount - i.PaidAmount, i.Customer.Email, i.Customer.FullName, i.CustomerId })
            .FirstAsync(ct);

        if (onlyIfOptedIn && await uow.Repository<NotificationPreference>().Query()
                .AnyAsync(p => p.UserId == invoice.CustomerId && !p.EmailReminders, ct))
            return null;
        var startOfToday = VietnamTime.ToUtc(VietnamTime.Today.ToDateTime(TimeOnly.MinValue));
        if (await uow.Repository<Reminder>().Query().AnyAsync(r =>
                r.InvoiceId == invoiceId && r.Template == template && r.SentAt >= startOfToday, ct))
            return null;

        var subject = template == ReminderTemplates.DueSoon
            ? $"StorEase: hóa đơn {invoice.InvoiceNumber} sắp đến hạn"
            : $"StorEase: nhắc thanh toán hóa đơn {invoice.InvoiceNumber}";
        var body = $"Xin chào {invoice.FullName},\n\nHóa đơn {invoice.InvoiceNumber} còn {Money.Round(invoice.Balance):N0} VND, "
            + $"hạn thanh toán {invoice.DueDate:dd/MM/yyyy}. Vui lòng thanh toán trên StorEase để tránh phí trễ hạn.\n\nStorEase";
        var delivered = await email.SendAsync(invoice.Email, subject, body, ct);

        var reminder = new Reminder
        {
            InvoiceId = invoiceId,
            Channel = ReminderChannel.Email,
            Template = template,
            SentAt = DateTime.UtcNow,
            Status = delivered ? ReminderStatus.Sent : ReminderStatus.Failed,
        };
        uow.Repository<Reminder>().Add(reminder);
        await uow.SaveChangesAsync(ct);
        return reminder.Status;
    }

    private async Task<bool> CanSeeAsync(int invoiceId, CancellationToken ct)
    {
        var invoices = uow.Repository<Invoice>().Query().Where(i => i.InvoiceId == invoiceId);
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return await invoices.AnyAsync(ct);
        var facilityIds = StaffScope.FacilityIdsOf(uow, currentUser.UserId);
        return await invoices.AnyAsync(i =>
            (i.Contract != null && facilityIds.Contains(i.Contract.StorageUnit.FacilityId))
            || (i.Reservation != null && facilityIds.Contains(i.Reservation.FacilityId)), ct);
    }
}
