namespace SafeFreeSpace.Infrastructure.Windows.Storage;

using SafeFreeSpace.Core.Models;

public sealed record CimPhysicalDiskInfo(
    uint DiskNumber,
    DriveMediaType MediaType,
    DriveBusType BusType,
    string? FriendlyName,
    string? HealthStatus);
