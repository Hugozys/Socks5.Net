using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Net;

namespace Sock5.Net.Common
{
    internal abstract class SockState : ISockState
    {
        protected readonly SockReader _sockReader;

        public StateType StateType => StateType.TRANSIT;

        public SockState(SockReader sockReader)
        {
            _sockReader = sockReader ?? throw new ArgumentNullException(nameof(sockReader));
        }
        public abstract StateReadResult DoRead(ref ReadOnlySequence<byte> sequence);
    }

    internal class VersionState : SockState
    {
        private readonly SockState _next;

        public VersionState(SockReader sockReader, SockState nextState) : base(sockReader)
        {
            _next = nextState;
        }

        public override StateReadResult DoRead(ref ReadOnlySequence<byte> sequence)
        {
            if (sequence.FirstSpan[0] != Constants.Version)
            {
                return StateReadResultHelper.ErrorResult(ErrorCode.InvalidVersionNumber);
            }
            sequence = sequence.Slice(sequence.GetPosition(1, sequence.Start));

            _sockReader.CurrentState = _next;

            return StateReadResult.SuccessResult;
        }
    }

    #region AuthMethods
    internal class NMethodsState : SockState
    {
        public NMethodsState(SockReader sockReader) : base(sockReader)
        {
        }

        public override StateReadResult DoRead(ref ReadOnlySequence<byte> sequence)
        {
            var span = sequence.FirstSpan;
            if (span[0] == 0x00)
            {
                return StateReadResultHelper.ErrorResult(ErrorCode.InvalidNMethods);
            }
            var nmethods = span[0];
            sequence = sequence.Slice(sequence.GetPosition(1, sequence.Start));
            _sockReader.CurrentState = new MethodsState(nmethods, _sockReader);

            return StateReadResult.SuccessResult;
        }
    }

    internal class MethodsState : SockState
    {
        private readonly byte _number;

        public MethodsState(byte number, SockReader sockReader) : base(sockReader)
        {
            _number = number;
        }

        public override StateReadResult DoRead(ref ReadOnlySequence<byte> sequence)
        {
            if (sequence.Length < _number)
            {
                return StateReadResult.PendingResult;
            }

            var authMethods = new HashSet<byte>();
            int remaining = _number;
            var it = sequence.GetEnumerator();
            do
            {
                var segment = it.Current;
                var span = segment.Span;
                if (remaining < segment.Length)
                {
                    span = span[..remaining];
                }
                authMethods.UnionWith(span.ToArray());
                remaining = remaining < segment.Length ? 0 : (remaining - segment.Length);
            }
            while (remaining != 0 && it.MoveNext());

            sequence = sequence.Slice(sequence.GetPosition(_number, sequence.Start));
            authMethods.IntersectWith(Constants.PreDefinedAuthMethods);
            _sockReader.CurrentState = new AuthDoneState(_sockReader, authMethods.ToImmutableHashSet());
            return StateReadResult.SuccessResult;
        }
    }

    #endregion

    #region RequestMessage
    internal class CmdState : SockState
    {
        public CmdState(SockReader sockReader) : base(sockReader)
        {
        }
        public override StateReadResult DoRead(ref ReadOnlySequence<byte> sequence)
        {
            var cmd = sequence.FirstSpan[0];
            if (!Constants.CMD.CmdSet.Contains(cmd))
            {
                return StateReadResultHelper.ErrorResult(ErrorCode.InvalidCmd);
            }
            sequence = sequence.Slice(sequence.GetPosition(1, sequence.Start));


            _sockReader.CurrentState = new RsvState(_sockReader);
            _sockReader.RequestBuilder.WithCmd(cmd);
            return StateReadResult.SuccessResult;
        }
    }

    internal class RsvState : SockState
    {
        public RsvState(SockReader sockReader) : base(sockReader)
        {
        }

        public override StateReadResult DoRead(ref ReadOnlySequence<byte> sequence)
        {
            if (sequence.FirstSpan[0] != Constants.Rsv)
            {
                return StateReadResultHelper.ErrorResult(ErrorCode.InvalidRsv);
            }
            sequence = sequence.Slice(sequence.GetPosition(1, sequence.Start));

            _sockReader.CurrentState = new AtypState(_sockReader);

            return StateReadResult.SuccessResult;
        }
    }

    internal class AtypState : SockState
    {

        public AtypState(SockReader sockReader) : base(sockReader)
        {
        }

        public override StateReadResult DoRead(ref ReadOnlySequence<byte> sequence)
        {
            var addrType = sequence.FirstSpan[0];
            if (!Constants.AddrType.AddrTypeSet.Contains(addrType))
            {
                return StateReadResultHelper.ErrorResult(ErrorCode.InvalidAddrType);
            }
            sequence = sequence.Slice(sequence.GetPosition(1, sequence.Start));

            _sockReader.RequestBuilder.WithAddrType(addrType);
            _sockReader.CurrentState = new DstAddrState(_sockReader, addrType);
            return StateReadResult.SuccessResult;
        }
    }

    internal class DstAddrState : SockState
    {
        private readonly byte _type;
        public DstAddrState(SockReader sockePipe, byte type) : base(sockePipe)
        {
            _type = type;
        }
        public override StateReadResult DoRead(ref ReadOnlySequence<byte> sequence)
        {
            int domainLen = _type switch
            {
                Constants.AddrType.Domain => sequence.FirstSpan[0],
                Constants.AddrType.IPV4 => 4,
                _ => 16
            };
            
            if (_type == Constants.AddrType.Domain)
            {
                sequence = sequence.Slice(sequence.GetPosition(1, sequence.Start));
            }
            _sockReader.CurrentState = new DomainNameState(_sockReader, domainLen);
            return StateReadResult.SuccessResult;
        }
    }

    internal class DomainNameState : SockState
    {
        private int _domainLen;
        public DomainNameState(SockReader sockReader, int domainLen) : base(sockReader)
        {
            _domainLen = domainLen;
        }
        public override StateReadResult DoRead(ref ReadOnlySequence<byte> sequence)
        {
            if (sequence.Length < _domainLen)
            {
                return StateReadResult.PendingResult;
            }
            int remaining = _domainLen;
            var domain = new byte[remaining];
            var domainSpan = domain.AsSpan();
            var it = sequence.GetEnumerator();
            do
            {
                var segment = it.Current;
                var span = segment.Span;
                if (remaining < segment.Length)
                {
                    span = span[..remaining];
                }
                span.CopyTo(domainSpan);
                domainSpan = domainSpan.Slice(span.Length);
                remaining = remaining < segment.Length ? 0 : (remaining - segment.Length);
            } while (remaining != 0 && it.MoveNext());

            sequence = sequence.Slice(sequence.GetPosition(_domainLen, sequence.Start));
            _sockReader.RequestBuilder.WithHost(domain);
            _sockReader.CurrentState = new DstPortState(_sockReader);
            return StateReadResult.SuccessResult;
        }
    }

    internal class DstPortState : SockState
    {
        public DstPortState(SockReader sockePipe) : base(sockePipe)
        {
        }

        public override StateReadResult DoRead(ref ReadOnlySequence<byte> sequence)
        {
            if (sequence.Length < 2)
            {
                return StateReadResult.PendingResult;
            }
            Span<byte> port = stackalloc byte[2];
            port[0] = sequence.FirstSpan[0];
            port[1] = sequence.Slice(sequence.GetPosition(1)).FirstSpan[0];
            var byteorderPort = BitConverter.ToInt16(port);
            var hostorderPort = (ushort) IPAddress.NetworkToHostOrder(byteorderPort);
            sequence = sequence.Slice(sequence.GetPosition(2, sequence.Start));
            _sockReader.RequestBuilder.WithPort(hostorderPort);
            _sockReader.CurrentState = new RequestMessageDoneState(_sockReader);
            return StateReadResult.SuccessResult;
        }
    }

    #endregion
}