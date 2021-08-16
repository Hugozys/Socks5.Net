using System.Buffers;

namespace Sock5.Net.Common
{
    public enum StateType
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