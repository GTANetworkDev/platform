using System;

namespace GTANetwork.Util
{
    public class Interpolator<T>
    {
        private const int maxElements = 64;
        private VectorMap[] m_nodes = new VectorMap[maxElements];
        private uint m_uiStartIdx;
        private uint m_uiEndIdx;
        private uint m_uiSize;

        private uint Index(uint uiIndex)
        {
            return (uiIndex%maxElements);
        }

        private uint Size()
        {
            return m_uiSize;
        }


        public void Push(T newData, int ticks)
        {
            uint uiIndex = Index(m_uiEndIdx + 1);
            m_nodes[m_uiEndIdx].Data = newData;
            m_nodes[m_uiEndIdx].Ticks = ticks;
            m_uiEndIdx = uiIndex;

            if (Size() < maxElements)
                ++m_uiSize;
            else
                m_uiStartIdx = Index(m_uiStartIdx + 1);
        }

        public void Pop()
        {
            if (Size() > 0)
            {
                m_uiStartIdx = Index(m_uiStartIdx + 1);
                m_uiSize--;
            }
        }

        public bool Evaluate(int ulTime, ref T output)
        {
            if (Size() == 0) return false;

            // Time later than newest point, so use the newest point
            if (ulTime >= m_nodes[Index(m_uiEndIdx - 1)].Ticks)
            {
                output = m_nodes[Index(m_uiEndIdx - 1)].Data;
            }
            // Time earlier than oldest point, so use the oldest point
            else if (ulTime <= m_nodes[m_uiStartIdx].Ticks)
            {
                output = m_nodes[m_uiStartIdx].Data;
            }
            else
            {
                // Find the two points either side and interpolate

                uint uiCurrent = Index(m_uiStartIdx + 1);
                for (; uiCurrent != m_uiEndIdx; uiCurrent = Index(uiCurrent + 1))
                {
                    if (ulTime < m_nodes[uiCurrent].Ticks)
                        return Eval(m_nodes[Index(uiCurrent - 1)], m_nodes[uiCurrent], ulTime, ref output);
                }
            }

            return true;
        }

        public int GetOldestEntry(ref T output)
        {
            if (Size() > 0)
            {
                output = m_nodes[m_uiStartIdx].Data;
                return m_nodes[m_uiStartIdx].Ticks;
            }
            else
            {
                return 0;
            }
        }


        public void Clear()
        {
            m_uiStartIdx = 0;
            m_uiEndIdx = 0;
            m_uiSize = 0;
        }

        protected virtual bool Eval(VectorMap left, VectorMap right, int ulTimeEval, ref T output)
        {
            // Check for being the same or maybe wrap around
            if (left.Ticks >= right.Ticks)
            {
                output = right.Data;
                return true;
            }

            // Find the relative position of ulTimeEval between right.Ticks and left.Ticks
            float fAlpha = Util.Unlerp(left.Ticks, ulTimeEval, right.Ticks);

            // Lerp between right.pos and left.pos
            output = Util.Lerp(left.Data, right.Data, fAlpha);
            return true;
        }


        protected struct VectorMap
        {
            public int Ticks;
            public T Data;

            public VectorMap(int t, T v)
            {
                Ticks = t;
                Data = v;
            }

            public VectorMap(T t)
            {
                Data = t;
                Ticks = Environment.TickCount;
            }
        }
    }
}