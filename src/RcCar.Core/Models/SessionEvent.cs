namespace RcCar.Core.Models;

public sealed record SessionEvent(DateTimeOffset Timestamp, SessionEventKind Kind, string Message);
