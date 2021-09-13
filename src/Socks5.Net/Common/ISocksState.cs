using System.Buffers;

namespace Socks5.Net.Common
{
    internal enum StateType
    {
        TRANSIT,
        DONE
    }

    internal interface ISocksState
    {
        StateType StateType{ get; }

        StateReadResult DoRead(ref ReadOnlySequence<byte> sequence);
    }
}