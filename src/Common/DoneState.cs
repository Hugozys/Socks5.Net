using System;
using System.Buffers;
using System.Collections.Immutable;

namespace Sock5.Net.Common
{
    internal abstract class DoneState : ISockState
    {
        protected readonly SockReader _sockReader;

        public StateType StateType => StateType.DONE;

        public DoneState(SockReader sockReader)
        {
            _sockReader = sockReader ?? throw new ArgumentNullException(nameof(sockReader));
        }

        public StateReadResult DoRead(ref ReadOnlySequence<byte> sequence) => StateReadResult.SuccessResult;
    }

    internal class AuthDoneState : DoneState
    {
        public ImmutableHashSet<byte> AuthMethods{ get; }

        public AuthDoneState(SockReader sockReader, ImmutableHashSet<byte> authMethods): base(sockReader)
        {
            AuthMethods = authMethods;
        }
    }

    internal class RequestMessageDoneState : DoneState
    {
        public RequestMessage RequestMessage { get; }

        public RequestMessageDoneState(SockReader sockReader): base(sockReader)
        {
            RequestMessage = sockReader.RequestBuilder.ToRequestMessage();
        }
    }
}