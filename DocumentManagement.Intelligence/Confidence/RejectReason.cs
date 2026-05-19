namespace DocumentManagement.Intelligence.Confidence;

public sealed record RejectReason(
    string Code,
    string Message,
    bool IsHardReject = false)
{
    public static RejectReason Hard(string code, string message) => new(code, message, true);

    public static RejectReason Soft(string code, string message) => new(code, message, false);
}
