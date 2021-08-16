namespace Sock5.Net
{
    public enum ErrorCode
    {
        InvalidVersionNumber,
        InvalidNMethods,
        InComplete,
        InvalidCmd,
        InvalidRsv,
        InvalidAddrType,
        InvalidHostName,
        UnreachableHost,
        Cancelled
    }
}