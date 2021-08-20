namespace Sock5.Net.Common
{
    public enum ErrorCode
    {
        InvalidVersionNumber,
        InvalidNMethods,
        InComplete,
        InvalidCmd,
        UnsupportedCmd,
        UnsupportedAuth,
        NotAllowedByRuleSet,
        InvalidRsv,
        InvalidAddrType,
        InvalidHostName,
        UnreachableHost,
        Cancelled
    }
}