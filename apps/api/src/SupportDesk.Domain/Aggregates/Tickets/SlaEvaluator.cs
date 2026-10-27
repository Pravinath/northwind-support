namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date. SLA status is always derived from
/// the ticket's dates and the current time - never stored.
/// </summary>
public static class SlaEvaluator
{
    public static SlaStatus Evaluate(DateTime? dueAtUtc, DateTime? resolvedAtUtc, DateTime nowUtc, double atRiskThresholdPercent = 25, double? windowHours = null) =>
        dueAtUtc is null ? SlaStatus.NotApplicable
        : resolvedAtUtc is not null
            ? (resolvedAtUtc <= dueAtUtc ? SlaStatus.Met : SlaStatus.Breached)
        : nowUtc > dueAtUtc ? SlaStatus.Breached
        : windowHours is > 0 && (dueAtUtc.Value - nowUtc).TotalHours <= windowHours.Value * atRiskThresholdPercent / 100
            ? SlaStatus.AtRisk
        : SlaStatus.WithinSla;
}
