namespace GTANetworkServer.Constant
{
    public struct Color
    {
        public Color(int r, int g, int b)
        {
            red = r;
            green = g;
            blue = b;
            alpha = 255;
        }

        public Color(int r, int g, int b, int a)
        {
            red = r;
            green = g;
            blue = b;
            alpha = a;
        }

        public int red;
        public int green;
        public int blue;
        public int alpha;
    }
}