namespace SupportDesk.Application.Abstractions;

public sealed class TicketTriageOptions
{
    public Dictionary<string, double> BaseWindowHours { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public double PremiumCustomerMultiplier { get; init; } = 0.5;
    public double MinimumWindowHours { get; init; } = 1;
    public double AtRiskThresholdPercent { get; init; } = 25;
    public double WindowHours(string priority) => BaseWindowHours.TryGetValue(priority, out var hours) ? hours : 24;
}
