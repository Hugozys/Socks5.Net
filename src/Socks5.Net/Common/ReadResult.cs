using System;

namespace Socks5.Net.Common
{
    internal enum ReadStatus
    {
        Success,

        Pending,

        Failure
    }

    readonly internal struct StateReadResult
    {
        public ReadStatus Status { get; }

        public ErrorCode? Reason { get; }

        public static StateReadResult SuccessResult => new StateReadResult(ReadStatus.Success);

        public static StateReadResult PendingResult => new StateReadResult(ReadStatus.Pending);

        public StateReadResult(ReadStatus status, ErrorCode? code = null)
        {
            Status = status;
            Reason = code;
        }
    }

    internal static class StateReadResultHelper
    {
        public static StateReadResult ErrorResult(ErrorCode code) => new StateReadResult(ReadStatus.Failure, code);
    }

    internal static class StateReadResultExtension
    {
        public static SocksResponse ToSocksResponse(this StateReadResult state) => state.Status switch
        {
            ReadStatus.Failure => SocksResponseHelper.ErrorResult(state.Reason!.Value),
            ReadStatus.Success => SocksResponse.SuccessResult,
            _ =>  throw new ArgumentException($"Can't convert {nameof(state)} of pending status to SocksResponse type")
        };

        public static SocksResponse<T> ToSockResposne<T>(this StateReadResult state) => state.Status switch
        {
            ReadStatus.Failure => SocksResponseHelper.ErrorResult<T>(state.Reason!.Value),
            _ =>  throw new ArgumentException($"Can't convert {nameof(state)} of {state.Status} status to genric SocksResponse type")
        };
    }

}