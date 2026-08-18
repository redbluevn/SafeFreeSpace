namespace SafeFreeSpace.Infrastructure.Windows.Pipes;

using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

internal static class PipeSecurityHelper
{
    public static PipeSecurity CreateRestrictedPipeSecurity()
    {
        var pipeSecurity = new PipeSecurity();
        // Fail-closed: không xác định được SID người dùng hiện tại thì từ chối tạo pipe,
        // tuyệt đối không fallback sang Everyone (WorldSid).
        string currentUserSid = WindowsIdentity.GetCurrent().User?.Value
            ?? throw new InvalidOperationException("Cannot determine the current user SID; refusing to create the pipe ACL (fail-closed).");
        var userRule = new PipeAccessRule(
            new SecurityIdentifier(currentUserSid),
            PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize,
            AccessControlType.Allow);
        pipeSecurity.AddAccessRule(userRule);

        var systemRule = new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.ReadWrite | PipeAccessRights.Synchronize,
            AccessControlType.Allow);
        pipeSecurity.AddAccessRule(systemRule);

        pipeSecurity.SetOwner(new SecurityIdentifier(currentUserSid));
        return pipeSecurity;
    }
}
