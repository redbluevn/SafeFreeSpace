namespace SafeFreeSpace.Core.Models;

public enum DriveBusType
{
    Unknown = 0,
    Sata,
    Nvme,
    Usb,
    Sas,
    Raid,
    Virtual,
    StorageSpaces
}
