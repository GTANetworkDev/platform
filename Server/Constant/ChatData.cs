using ProtoBuf;

namespace GTANetworkServer.Constant
{
    [ProtoContract]
    internal class ChatData
    {
        [ProtoMember(1)]
        public long Id { get; set; }
        [ProtoMember(2)]
        public string Sender { get; set; }
        [ProtoMember(3)]
        public string Message { get; set; }
    }
}