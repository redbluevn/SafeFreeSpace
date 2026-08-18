namespace SafeFreeSpace.Core.Interfaces;

using SafeFreeSpace.Core.Models;

public interface IVolumeInventory
{
    Task<IReadOnlyList<VolumeIdentity>> GetVolumesAsync(CancellationToken cancellationToken = default);

    Task<VolumeIdentity?> RefreshVolumeAsync(string driveLetter, CancellationToken cancellationToken = default);
}
