using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

public sealed class TicketQueriesTests
{
    [Fact]
    public async Task AppliesExactPriorityBeforeCountingAndPaging()
    {
        await using var database = await TestDatabase.CreateAsync();

        var result = await database.Queries.GetPagedAsync(
            new TicketQuery { Priority = TicketPriority.High, PageSize = 1 }.Normalized(),
            CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
        Assert.All(result.Items, ticket => Assert.Equal(TicketPriority.High, ticket.Priority));
    }

    [Fact]
    public async Task CombinesSuppliedFiltersWithAnd()
    {
        await using var database = await TestDatabase.CreateAsync();

        var result = await database.Queries.GetPagedAsync(
            new TicketQuery
            {
                Status = TicketStatus.InProgress,
                Priority = TicketPriority.High,
                CategoryId = database.BillingCategoryId,
                CustomerId = database.ContosoCustomerId
            }.Normalized(),
            CancellationToken.None);

        var ticket = Assert.Single(result.Items);
        Assert.Equal("TCK-0043", ticket.Reference);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task SearchesTrimmedCustomerNameInDatabase()
    {
        await using var database = await TestDatabase.CreateAsync();

        var result = await database.Queries.GetPagedAsync(
            new TicketQuery { Search = "  Contoso  " }.Normalized(),
            CancellationToken.None);

        Assert.Equal(2, result.TotalCount);
        Assert.All(result.Items, ticket => Assert.Equal("Contoso Ltd", ticket.Customer.Name));
    }

    [Fact]
    public async Task FiltersByAssignedAgentAndUnassignedOnly()
    {
        await using var database = await TestDatabase.CreateAsync();

        var assigned = await database.Queries.GetPagedAsync(
            new TicketQuery { AssignedAgentId = database.AlexAgentId }.Normalized(),
            CancellationToken.None);
        var unassigned = await database.Queries.GetPagedAsync(
            new TicketQuery { UnassignedOnly = true }.Normalized(),
            CancellationToken.None);

        Assert.Equal("TCK-0041", Assert.Single(assigned.Items).Reference);
        Assert.Equal("TCK-0043", Assert.Single(unassigned.Items).Reference);
    }

    [Fact]
    public async Task FiltersAtRiskSlaInDatabaseBeforePaging()
    {
        await using var database = await TestDatabase.CreateAsync();
        var result = await database.Queries.GetPagedAsync(
            new TicketQuery { SlaStatus = SlaStatus.AtRisk, PageSize = 1 }.Normalized(), CancellationToken.None);
        Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("TCK-0042", result.Items[0].Reference);
        Assert.Equal(SlaStatus.AtRisk, result.Items[0].SlaStatus);
    }

    private sealed class TestDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private TestDatabase(SqliteConnection connection, SupportDbContext context)
        {
            _connection = connection;
            Context = context;
            Queries = new TicketQueries(context, new FixedClock(), new TicketTriageOptions
            {
                BaseWindowHours = new Dictionary<string, double> { ["Low"] = 72, ["Medium"] = 24, ["High"] = 8, ["Critical"] = 4 }
            });
        }

        public SupportDbContext Context { get; }

        public TicketQueries Queries { get; }

        public int ContosoCustomerId { get; private init; }

        public int BillingCategoryId { get; private init; }

        public int AlexAgentId { get; private init; }

        public static async Task<TestDatabase> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<SupportDbContext>()
                .UseSqlite(connection)
                .Options;
            var context = new SupportDbContext(options);
            await context.Database.EnsureCreatedAsync();

            var contoso = new Customer(
                "Contoso Ltd", "support@contoso.example", null, CustomerTier.Premium, FixedClock.DefaultNow);
            var tailspin = new Customer(
                "Tailspin Toys", "help@tailspin.example", null, CustomerTier.Standard, FixedClock.DefaultNow);
            var general = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);
            var billing = new Category("Billing", requiresSpecialist: true, forcesCriticalPriority: false);
            var alex = new Agent("Alex Turner", "alex@example.com", maxOpenTickets: 10, FixedClock.DefaultNow);
            var sara = new Agent("Sara Lindqvist", "sara@example.com", maxOpenTickets: 6, FixedClock.DefaultNow);

            context.AddRange(contoso, tailspin, general, billing, alex, sara);
            await context.SaveChangesAsync();

            context.AddRange(
                CreateTicket("TCK-0041", "Account access issue", contoso.Id, general.Id, alex.Id,
                    TicketPriority.High, TicketStatus.InProgress),
                CreateTicket("TCK-0042", "Invoice export problem", tailspin.Id, billing.Id, sara.Id,
                    TicketPriority.Low, TicketStatus.New),
                CreateTicket("TCK-0043", "Payment outage", contoso.Id, billing.Id, null,
                    TicketPriority.High, TicketStatus.InProgress));
            await context.SaveChangesAsync();

            return new TestDatabase(connection, context)
            {
                ContosoCustomerId = contoso.Id,
                BillingCategoryId = billing.Id,
                AlexAgentId = alex.Id
            };
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }

        private static Ticket CreateTicket(
            string reference,
            string title,
            int customerId,
            int categoryId,
            int? agentId,
            TicketPriority priority,
            TicketStatus status)
        {
            var ticket = Ticket.Raise(
                reference,
                title,
                "A description long enough to be realistic.",
                customerId,
                categoryId,
                priority,
                FixedClock.DefaultNow);

            if (agentId is not null)
            {
                ticket.AssignTo(agentId, FixedClock.DefaultNow);
            }

            var due = priority == TicketPriority.Low
                ? FixedClock.DefaultNow.AddHours(10)
                : FixedClock.DefaultNow.AddHours(2);
            ticket.ApplyTriage(priority, agentId, due, FixedClock.DefaultNow);

            if (status != TicketStatus.New)
            {
                ticket.ChangeStatus(status, FixedClock.DefaultNow.AddMinutes(1));
            }

            return ticket;
        }
    }
}
