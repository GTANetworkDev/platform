namespace GTANetworkShared
{
    public struct NetHandle
    {
        public NetHandle(int handle)
        {
            Value = handle;
        }

        public override bool Equals(object obj)
        {
            return (obj as NetHandle?)?.Value == Value;
        }

        public static bool operator ==(NetHandle left, NetHandle right)
        {
            return left.Value == right.Value;
        }

        public static bool operator !=(NetHandle left, NetHandle right)
        {
            return left.Value != right.Value;
        }

        public override int GetHashCode()
        {
            return Value.GetHashCode();
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public bool IsNull { get { return Value == 0; } }

        public int Value { get; set; }
    }
}