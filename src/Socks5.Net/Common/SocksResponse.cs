using System;

namespace Socks5.Net.Common
{
    readonly public struct SocksResponse
    {
        public bool Success { get; }

        public ErrorCode? Reason { get; }

        public static SocksResponse SuccessResult => new(true);

        public SocksResponse(bool status, ErrorCode? reason = null)
        {
            Success = status;
            Reason = reason;
        }
    }

    readonly public struct SocksResponse<T>
    {
        public bool Success { get; }

        public ErrorCode? Reason { get; }

        public T? Payload { get; }

        public SocksResponse(bool status, ErrorCode? reason = null, T? payload = default )
        {
            Success = status;
            Reason = reason;
            Payload = payload;
        }
    }

    internal static class SocksResponseHelper
    {
        public static SocksResponse ErrorResult(ErrorCode code) => new(false, code);

        public static SocksResponse<T> ErrorResult<T>(ErrorCode code) => new(false, code);

        public static SocksResponse<T> SuccessResult<T>(T payload) => new(true, payload: payload);
    }

    internal static class SocksResponseExtensions
    {
        public static SocksResponse<T> ToGeneric<T>(this SocksResponse response, T? payload = default)
        {
            return response.Success switch
            {
                true => payload is null ? throw new ArgumentNullException(nameof(payload)) : SocksResponseHelper.SuccessResult(payload),
                _ => SocksResponseHelper.ErrorResult<T>(response.Reason!.Value)
            };
        }
    }
}