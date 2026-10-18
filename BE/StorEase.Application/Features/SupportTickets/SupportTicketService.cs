using Microsoft.EntityFrameworkCore;
using StorEase.Application.Common;
using StorEase.Application.Features.SupportTickets.Dtos;
using StorEase.Application.Interfaces;
using StorEase.Domain.Entities;
using StorEase.Domain.Enums;

namespace StorEase.Application.Features.SupportTickets;

public interface ISupportTicketService
{
    Task<Result<IReadOnlyList<TicketCategoryDto>>> ListCategoriesAsync(CancellationToken ct);
    Task<Result<TicketDto>> CreateAsync(CreateTicketRequest request, CancellationToken ct);
    Task<Result<TicketDto>> GetAsync(int ticketId, CancellationToken ct);
    Task<Result<IReadOnlyList<TicketMessageDto>>> ListMessagesAsync(int ticketId, CancellationToken ct);
    Task<Result<TicketMessageDto>> PostMessageAsync(int ticketId, PostTicketMessageRequest request, CancellationToken ct);
}

public class SupportTicketService(IUnitOfWork uow, ICurrentUser currentUser) : ISupportTicketService
{
    // A customer can raise a ticket about a unit they still hold under one of these contract states.
    private static readonly ContractStatus[] HeldStatuses =
        [ContractStatus.Active, ContractStatus.Overdue, ContractStatus.Suspended, ContractStatus.PendingHandover];

    public async Task<Result<IReadOnlyList<TicketCategoryDto>>> ListCategoriesAsync(CancellationToken ct)
    {
        IReadOnlyList<TicketCategoryDto> items = await uow.Repository<TicketCategory>().Query()
            .OrderBy(c => c.Name)
            .Select(c => new TicketCategoryDto(c.CategoryId, c.Name, c.DefaultPriority))
            .ToListAsync(ct);
        return Result<IReadOnlyList<TicketCategoryDto>>.Success(items);
    }

    public async Task<Result<TicketDto>> CreateAsync(CreateTicketRequest request, CancellationToken ct)
    {
        var category = await uow.Repository<TicketCategory>().Query()
            .FirstOrDefaultAsync(c => c.CategoryId == request.CategoryId, ct);
        if (category is null)
            return Error.NotFound("Ticket category not found.");

        int facilityId;
        if (request.StorageUnitId is { } unitId)
        {
            var customerId = currentUser.UserId;
            var held = await uow.Repository<Contract>().Query()
                .Where(c => c.CustomerId == customerId && c.StorageUnitId == unitId && HeldStatuses.Contains(c.Status))
                .Select(c => new { c.StorageUnit.FacilityId })
                .FirstOrDefaultAsync(ct);
            // Same 404 whether the unit does not exist or belongs to someone else.
            if (held is null)
                return Error.NotFound("You have no active contract on this storage unit.");
            facilityId = held.FacilityId;
            if (request.FacilityId is { } given && given != facilityId)
                return Error.Validation("The facility does not match the storage unit.");
        }
        else
        {
            facilityId = request.FacilityId!.Value;
            var open = await uow.Repository<Facility>().Query()
                .AnyAsync(f => f.FacilityId == facilityId && f.Status == FacilityStatus.Active, ct);
            if (!open)
                return Error.NotFound("Facility not found.");
        }

        var now = DateTime.UtcNow;
        var ticket = new SupportTicket
        {
            CustomerId = currentUser.UserId,
            FacilityId = facilityId,
            StorageUnitId = request.StorageUnitId,
            CategoryId = category.CategoryId,
            TicketCode = NewTicketCode(),
            Subject = request.Subject.Trim(),
            Priority = request.Priority ?? category.DefaultPriority,
            Status = SupportTicketStatus.New,
            CreatedAt = now,
            // The schema has no description column, so the description is the first message.
            TicketMessages =
            [
                new TicketMessage
                {
                    SenderId = currentUser.UserId,
                    Body = request.Description.Trim(),
                    IsInternalNote = false,
                    SentAt = now,
                },
            ],
        };
        uow.Repository<SupportTicket>().Add(ticket);
        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            var clash = await uow.Repository<SupportTicket>().Query()
                .AnyAsync(t => t.TicketCode == ticket.TicketCode && t.TicketId != ticket.TicketId, ct);
            if (!clash)
                throw;
            // The unique index on TicketCode rejected a random-code clash; the caller can simply retry.
            return Error.Conflict("Could not allocate a ticket code, please try again.");
        }

        return await GetAsync(ticket.TicketId, ct);
    }

    public async Task<Result<TicketDto>> GetAsync(int ticketId, CancellationToken ct)
    {
        var dto = await Project(Visible().Where(t => t.TicketId == ticketId)).FirstOrDefaultAsync(ct);
        // Someone else's ticket (or one outside the staff member's facilities) is a 404, so ids cannot be probed.
        return dto is null ? Error.NotFound("Ticket not found.") : dto;
    }

    public async Task<Result<IReadOnlyList<TicketMessageDto>>> ListMessagesAsync(int ticketId, CancellationToken ct)
    {
        if (!await Visible().AnyAsync(t => t.TicketId == ticketId, ct))
            return Error.NotFound("Ticket not found.");

        var messages = uow.Repository<TicketMessage>().Query().Where(m => m.TicketId == ticketId);
        if (currentUser.IsCustomer)
            messages = messages.Where(m => !m.IsInternalNote);

        IReadOnlyList<TicketMessageDto> items = await messages
            .OrderBy(m => m.SentAt).ThenBy(m => m.MessageId)
            .Select(m => new TicketMessageDto(
                m.MessageId, m.TicketId, m.SenderId, m.Sender.FullName, m.Body, m.IsInternalNote, m.SentAt))
            .ToListAsync(ct);
        return Result<IReadOnlyList<TicketMessageDto>>.Success(items);
    }

    public async Task<Result<TicketMessageDto>> PostMessageAsync(
        int ticketId, PostTicketMessageRequest request, CancellationToken ct)
    {
        var isCustomer = currentUser.IsCustomer;
        if (isCustomer && (request.IsInternalNote || request.NextStatus is not null))
            return Error.Forbidden("Only staff can write internal notes or set the ticket status.");

        var posted = await uow.InTransactionAsync<int>(async () =>
        {
            var ticket = await Visible().FirstOrDefaultAsync(t => t.TicketId == ticketId, ct);
            if (ticket is null)
                return Error.NotFound("Ticket not found.");
            if (ticket.Status == SupportTicketStatus.Closed)
                return Error.Conflict("The ticket is closed and no longer accepts messages.");

            // Same transition table as the queue; asking for the status the ticket already has is a no-op.
            if (!isCustomer && !request.IsInternalNote && request.NextStatus is { } wanted && wanted != ticket.Status &&
                TicketStatusRules.CheckMove(currentUser.Role, false, ticket.Status, wanted) is { } refused)
                return refused;

            var message = new TicketMessage
            {
                TicketId = ticket.TicketId,
                SenderId = currentUser.UserId,
                Body = request.Body.Trim(),
                IsInternalNote = request.IsInternalNote,
                SentAt = DateTime.UtcNow,
            };
            uow.Repository<TicketMessage>().Add(message);

            if (isCustomer)
            {
                // A customer reply re-opens a Resolved ticket and answers a WaitingForCustomer one.
                if (ticket.Status is SupportTicketStatus.WaitingForCustomer or SupportTicketStatus.Resolved)
                {
                    ticket.Status = SupportTicketStatus.InProgress;
                    ticket.ResolvedAt = null;
                }
            }
            else if (!request.IsInternalNote)
            {
                if (request.NextStatus is { } next && next != ticket.Status)
                {
                    ticket.Status = next;
                    if (next == SupportTicketStatus.Resolved)
                        ticket.ResolvedAt = DateTime.UtcNow;
                }
                else if (ticket.Status == SupportTicketStatus.New)
                    ticket.Status = SupportTicketStatus.InProgress;
            }

            await uow.SaveChangesAsync(ct);
            return message.MessageId;
        }, ct);

        if (!posted.IsSuccess)
            return posted.Error;

        var messageId = posted.Value;
        return await uow.Repository<TicketMessage>().Query()
            .Where(m => m.MessageId == messageId)
            .Select(m => new TicketMessageDto(
                m.MessageId, m.TicketId, m.SenderId, m.Sender.FullName, m.Body, m.IsInternalNote, m.SentAt))
            .FirstAsync(ct);
    }

    /// <summary>Customers: their own tickets. Admins and business managers: all. Other staff: their facilities.</summary>
    private IQueryable<SupportTicket> Visible()
    {
        var query = uow.Repository<SupportTicket>().Query();
        var userId = currentUser.UserId;
        if (currentUser.IsCustomer)
            return query.Where(t => t.CustomerId == userId);
        if (StaffScope.SeesAllFacilities(currentUser.Role))
            return query;

        var facilityIds = StaffScope.FacilityIdsOf(uow, userId);
        return query.Where(t => facilityIds.Contains(t.FacilityId));
    }

    private static IQueryable<TicketDto> Project(IQueryable<SupportTicket> source) =>
        source.Select(t => new TicketDto(
            t.TicketId,
            t.TicketCode,
            t.CustomerId,
            t.Customer.FullName,
            t.FacilityId,
            t.Facility.Name,
            t.StorageUnitId,
            t.StorageUnit != null ? t.StorageUnit.UnitCode : null,
            t.CategoryId,
            t.Category.Name,
            t.Subject,
            t.Priority,
            t.Status,
            t.AssignedTo,
            t.DueDate,
            t.CreatedAt,
            t.ResolvedAt));

    // ponytail: random suffix, ~1-in-a-million clash per day; a clash is reported as a 409 to retry. Switch to a sequence if volume grows.
    protected virtual string NewTicketCode() => $"TK{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(1_000_000):D6}";
}
