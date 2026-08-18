namespace SafeFreeSpace.Core.Models;

public enum OperationErrorCategory
{
    None,
    ElevationDenied,
    VolumeChanged,
    UnsupportedVolume,
    UnknownMediaType,
    VolumeLocked,
    VolumeReadOnly,
    VolumeDirty,
    ToolNotFound,
    ProcessStartFailed,
    ProcessExitedWithError,
    PipeAuthenticationFailed,
    Cancelled,
    AppDisconnected,
    Unexpected
}
