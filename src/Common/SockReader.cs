using System;
using System.Buffers;
using System.Collections.Immutable;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace Sock5.Net.Common
{
    public class SockReader
    {
        private ISockState? _currState;

        private readonly PipeReader _pipeReader;

        internal ISockState? CurrentState { get => _currState; set => _currState = value; }

        internal RequestMessage.Builder RequestBuilder { get; } = new RequestMessage.Builder();

        public SockReader(PipeReader reader)
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
        public ValueTask<SockResponse<ImmutableHashSet<byte>>> ReadAuthMethodsAsync(CancellationToken token = default) 
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
        public ValueTask<SockResponse<RequestMessage>> ReadRequestMessageAsync(CancellationToken token = default) 
            => ReadMessageAsync<RequestMessage, RequestMessageDoneState>(
                new VersionState(this, nextState: new CmdState(this)), 
                x => x.RequestMessage, 
                token);

        internal async ValueTask<SockResponse<T>> ReadMessageAsync<T, U>(ISockState initialState, Func<U, T> extractor, CancellationToken token) where U: DoneState
        {
            _currState = initialState;
            while(true)
            {
                var readResult = await _pipeReader.ReadAsync(token);

                if (readResult.IsCanceled)
                {
                    return SockResponseHelper.ErrorResult<T>(ErrorCode.Cancelled);
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
                            return SockResponseHelper.SuccessResult(result!);
                        }
                    }

                    if (readResult.IsCompleted)
                    {
                        return SockResponseHelper.ErrorResult<T>(ErrorCode.InComplete);
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