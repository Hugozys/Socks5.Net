using System;
using System.Buffers;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Socks5.Net.Common
{
    public class SocksReader
    {
        private ISocksState? _currState;

        private readonly PipeReader _pipeReader;

        internal ISocksState? CurrentState { get => _currState; set => _currState = value; }

        internal RequestMessage.Builder RequestBuilder { get; } = new RequestMessage.Builder();

        public SocksReader(PipeReader reader)
        {
            _pipeReader = reader ?? throw new ArgumentNullException(nameof(reader));
        }

        /*
         *  +----+----------+----------+
         *  |VER | NMETHODS | METHODS  |
         *  +----+----------+----------+
         *  | 1  |    1     | 1 to 255 |
         *  +----+----------+----------+
         */
        public ValueTask<SocksResponse<ImmutableHashSet<byte>>> ReadAuthMethodsAsync(CancellationToken token = default) 
            => ReadMessageAsync<ImmutableHashSet<byte>, AuthDoneState>(
                new VersionState(this, nextState: new NMethodsState(this)), 
                x => x.AuthMethods, 
                token);

        /*
         *  +----+-----+-------+------+----------+----------+
         *  |VER | CMD |  RSV  | ATYP | DST.ADDR | DST.PORT |
         *  +----+-----+-------+------+----------+----------+
         *  | 1  |  1  | X'00' |  1   | Variable |    2     |
         *  +----+-----+-------+------+----------+----------+
         */
        public ValueTask<SocksResponse<RequestMessage>> ReadRequestMessageAsync(CancellationToken token = default) 
            => ReadMessageAsync<RequestMessage, RequestMessageDoneState>(
                new VersionState(this, nextState: new CmdState(this)), 
                x => x.RequestMessage, 
                token);

        internal async ValueTask<SocksResponse<T>> ReadMessageAsync<T, U>(ISocksState initialState, Func<U, T> extractor, CancellationToken token) where U: DoneState
        {
            _currState = initialState;
            while(true)
            {
                var readResult = await _pipeReader.ReadAsync(token);

                if (readResult.IsCanceled)
                {
                    return SocksResponseHelper.ErrorResult<T>(ErrorCode.Cancelled);
                }

                var buffer = readResult.Buffer;
                try
                {
                    while(true)
                    {
                        var status = ParseMethod(ref buffer, extractor, out var result);

                        if (status.Status == ReadStatus.Failure)
                        {
                            return status.ToSockResposne<T>();
                        }
                        if (status.Status == ReadStatus.Pending)
                        {
                            break;
                        }
                        if (status.Status == ReadStatus.Success && _currState.StateType == StateType.DONE)
                        {
                            _currState = null;
                            return SocksResponseHelper.SuccessResult(result!);
                        }
                    }

                    if (readResult.IsCompleted)
                    {
                        return SocksResponseHelper.ErrorResult<T>(ErrorCode.InComplete);
                    }
                } 
                catch(Exception)
                {
                    throw;
                }
                finally
                {
                    _pipeReader.AdvanceTo(buffer.Start, buffer.End);
                } 
            }
        }

        private StateReadResult ParseMethod<T, U>(ref ReadOnlySequence<byte> sequence, Func<U, T> extractor, out T? result) where U: DoneState
        {
            result = default;

            if (sequence.IsEmpty)
            {
                return StateReadResult.PendingResult;
            }
            var stateReadResult = _currState!.DoRead(ref sequence);
            if (stateReadResult.Status == ReadStatus.Success && _currState.StateType == StateType.DONE)
            {
                result = extractor((_currState as U)!);
            }
            return stateReadResult;
        }
    }
}