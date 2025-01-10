using AutoMapper;
using LocalVenue.Core;
using LocalVenue.Core.Entities;
using LocalVenue.Core.Services;
using LocalVenue.Helpers;
using LocalVenue.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LocalVenue.Services;

public class TicketService(IDbContextFactory<VenueContext> contextFactory, IMapper mapper) : GenericCRUDService<Ticket>(contextFactory), ITicketService
{
    private readonly IDbContextFactory<VenueContext> _contextFactory = contextFactory;

    public async Task<Ticket> GetTicket(long id)
    {
        return await base.GetItem(id, ticket => ticket.Show!, ticket => ticket.Customer!, ticket => ticket.Seat!);
    }

    public async Task<Ticket> AddTicket(Ticket ticket, VenueContext? context = null)
    {
        if (context == null)
        {
            context = await _contextFactory.CreateDbContextAsync();
        }

        //Check if the ticket's seat, customer, and show exist
        if (await context.Seats.FindAsync(ticket.SeatId) == null)
        {
            throw new ArgumentException($"Seat with id '{ticket.SeatId}' does not exist");
        }
        if (!string.IsNullOrWhiteSpace(ticket.CustomerId))
        {
            if (await context.Users.FindAsync(ticket.CustomerId) == null)
            {
                throw new ArgumentException($"Customer with id '{ticket.CustomerId}' does not exist");
            }
        }
        else
        {
            ticket.CustomerId = null;
        }

        var show = await context.Shows.FindAsync(ticket.ShowId);
        if (show == null)
        {
            throw new ArgumentException($"Show with id '{ticket.ShowId}' does not exist");
        }

        //Check if the ticket for show and seat already exists
        if (await context.Tickets.AnyAsync(t => t.ShowId == ticket.ShowId && t.SeatId == ticket.SeatId))
        {
            throw new ArgumentException($"Ticket for show '{ticket.ShowId}' already has seat '{ticket.SeatId}' assigned");
        }

        // map some stuff for before price calculation
        var seat = await context.Seats.FindAsync(ticket.SeatId);
        var webSeat = mapper.Map<Web.Models.Seat>(seat);
        var webModelsShow = mapper.Map<Web.Models.Show>(show);
        var webModelTicket = mapper.Map<Web.Models.Ticket>(ticket);
        webModelTicket.Seat = webSeat;
        ticket.Price = ShowPriceCalculator.CalculatePrice(webModelsShow, webModelTicket, show.OpeningNight);

        return await base.AddItem(ticket, ticket => ticket.Show!, ticket => ticket.Customer!, ticket => ticket.Seat!);
    }

    public async Task<Ticket> UpdateTicket(Ticket ticket)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        if (await context.Seats.FindAsync(ticket.SeatId) == null)
        {
            throw new ArgumentException($"Seat with id '{ticket.SeatId}' does not exist");
        }
        if (!string.IsNullOrWhiteSpace(ticket.CustomerId))
        {
            if (await context.Users.FindAsync(ticket.CustomerId) == null)
            {
                throw new ArgumentException($"Customer with id '{ticket.CustomerId}' does not exist");
            }
        }
        else
        {
            ticket.CustomerId = null;
        }
        if (await context.Shows.FindAsync(ticket.ShowId) == null)
        {
            throw new ArgumentException($"Show with id '{ticket.ShowId}' does not exist");
        }

        return await base.UpdateItem(ticket, ticket => ticket.Show!, ticket => ticket.Customer!, ticket => ticket.Seat!);
    }

    public async Task<Ticket> DeleteTicket(long id)
    {
        return await base.DeleteItem(id, ticket => ticket.Show!, ticket => ticket.Customer!, ticket => ticket.Seat!);
    }
}