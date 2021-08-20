using System.Buffers;

namespace Sock5.Net.Common
{
    internal enum StateType
    {
        TRANSIT,
        DONE
    }

    internal interface ISockState
    {
        StateType StateType{ get; }

        StateReadResult DoRead(ref ReadOnlySequence<byte> sequence);
    }
}