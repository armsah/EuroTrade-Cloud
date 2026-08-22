using System.Diagnostics;

namespace EuroTrade.Application.Telemetry;

public static class EuroTradeActivitySource
{
    public const string Name = "EuroTrade.Application";

    public static readonly ActivitySource Source = new(Name);
}