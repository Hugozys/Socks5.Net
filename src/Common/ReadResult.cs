using System;

namespace Sock5.Net.Common
{
    public enum ReadStatus
    {
        Success,

        Pending,

        Failure
    }

    readonly public struct StateReadResult
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

    public static class StateReadResultHelper
    {
        public static StateReadResult ErrorResult(ErrorCode code) => new StateReadResult(ReadStatus.Failure, code);
    }

    public static class StateReadResultExtension
    {
        public static SockResponse ToSockResponse(this StateReadResult state) => state.Status switch
        {
            ReadStatus.Failure => SockResponseHelper.ErrorResult(state.Reason!.Value),
            ReadStatus.Success => SockResponse.SuccessResult,
            _ =>  throw new ArgumentException($"Can't convert {nameof(state)} of pending status to SockResponse type")
        };

        public static SockResponse<T> ToSockResposne<T>(this StateReadResult state) => state.Status switch
        {
            ReadStatus.Failure => SockResponseHelper.ErrorResult<T>(state.Reason!.Value),
            _ =>  throw new ArgumentException($"Can't convert {nameof(state)} of {state.Status} status to genric SockResponse type")
        };
    }

}