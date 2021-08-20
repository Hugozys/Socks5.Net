using System;

namespace Sock5.Net.Common
{
    readonly public struct SockResponse
    {
        public bool Success { get; }

        public ErrorCode? Reason { get; }

        public static SockResponse SuccessResult => new(true);

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

    internal static class SockResponseHelper
    {
        public static SockResponse ErrorResult(ErrorCode code) => new(false, code);

        public static SockResponse<T> ErrorResult<T>(ErrorCode code) => new(false, code);

        public static SockResponse<T> SuccessResult<T>(T payload) => new(true, payload: payload);
    }

    internal static class SockResponseExtensions
    {
        public static SockResponse<T> ToGeneric<T>(this SockResponse response, T? payload = default)
        {
            return response.Success switch
            {
                true => payload is null ? throw new ArgumentNullException(nameof(payload)) : SockResponseHelper.SuccessResult(payload),
                _ => SockResponseHelper.ErrorResult<T>(response.Reason!.Value)
            };
        }
    }
}