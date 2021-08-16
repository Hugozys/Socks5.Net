using System;

namespace Sock5.Net.Common
{
    readonly public struct SockResponse
    {
        public bool Success { get; }

        public ErrorCode? Reason { get; }

        public static SockResponse SuccessResult => new SockResponse(true);

        public SockResponse(bool status, ErrorCode? reason = null)
        {
            Success = status;
            Reason = reason;
        }
    }

    readonly public struct SockResponse<T>
    {
        public bool Success { get; }

        public ErrorCode? Reason { get; }

        public T? Payload { get; }

        public SockResponse(bool status, ErrorCode? reason = null, T? payload = default )
        {
            Success = status;
            Reason = reason;
            Payload = payload;
        }
    }

    public static class SockResponseHelper
    {
        public static SockResponse ErrorResult(ErrorCode code) => new SockResponse(false, code);

        public static SockResponse<T> ErrorResult<T>(ErrorCode code) => new SockResponse<T>(false, code);

        public static SockResponse<T> SuccessResult<T>(T payload) => new SockResponse<T>(true, payload: payload);
    }

    public static class SockResponseExtensions
    {
        public static SockResponse<T> ToGeneric<T>(this SockResponse response, T? payload = default)
        {
            return response.Success switch
            {
                true => payload is null ? throw new ArgumentNullException(nameof(payload)) : SockResponseHelper.SuccessResult<T>(payload),
                _ => SockResponseHelper.ErrorResult<T>(response.Reason!.Value)
            };
        }
    }
}