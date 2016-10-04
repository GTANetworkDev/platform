using System;
using System.Collections.Generic;
using System.Reflection;
using GTANetworkServer.ArrayExtensions;

namespace GTANetworkServer
{
    public static class objectExtensions
    {
        private static readonly MethodInfo CloneMethod = typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance);

        public static bool IsPrimitive(this Type type)
        {
            if (type == typeof(String)) return true;
            return (type.IsValueType & type.IsPrimitive);
        }

        public static object Copy(this object originalobject)
        {
            return InternalCopy(originalobject, new Dictionary<object, object>(new ReferenceEqualityComparer()));
        }
        private static object InternalCopy(object originalobject, IDictionary<object, object> visited)
        {
            if (originalobject == null) return null;
            var typeToReflect = originalobject.GetType();
            if (IsPrimitive(typeToReflect)) return originalobject;
            if (visited.ContainsKey(originalobject)) return visited[originalobject];
            if (typeof(Delegate).IsAssignableFrom(typeToReflect)) return null;
            var cloneobject = CloneMethod.Invoke(originalobject, null);
            if (typeToReflect.IsArray)
            {
                var arrayType = typeToReflect.GetElementType();
                if (IsPrimitive(arrayType) == false)
                {
                    Array clonedArray = (Array)cloneobject;
                    clonedArray.ForEach((array, indices) => array.SetValue(InternalCopy(clonedArray.GetValue(indices), visited), indices));
                }

            }
            visited.Add(originalobject, cloneobject);
            CopyFields(originalobject, visited, cloneobject, typeToReflect);
            RecursiveCopyBaseTypePrivateFields(originalobject, visited, cloneobject, typeToReflect);
            return cloneobject;
        }

        private static void RecursiveCopyBaseTypePrivateFields(object originalobject, IDictionary<object, object> visited, object cloneobject, Type typeToReflect)
        {
            if (typeToReflect.BaseType != null)
            {
                RecursiveCopyBaseTypePrivateFields(originalobject, visited, cloneobject, typeToReflect.BaseType);
                CopyFields(originalobject, visited, cloneobject, typeToReflect.BaseType, BindingFlags.Instance | BindingFlags.NonPublic, info => info.IsPrivate);
            }
        }

        private static void CopyFields(object originalobject, IDictionary<object, object> visited, object cloneobject, Type typeToReflect, BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy, Func<FieldInfo, bool> filter = null)
        {
            foreach (FieldInfo fieldInfo in typeToReflect.GetFields(bindingFlags))
            {
                if (filter != null && filter(fieldInfo) == false) continue;
                if (IsPrimitive(fieldInfo.FieldType)) continue;
                var originalFieldValue = fieldInfo.GetValue(originalobject);
                var clonedFieldValue = InternalCopy(originalFieldValue, visited);
                fieldInfo.SetValue(cloneobject, clonedFieldValue);
            }
        }
        public static T Copy<T>(this T original)
        {
            return (T)Copy((object)original);
        }
    }

    public class ReferenceEqualityComparer : EqualityComparer<object>
    {
        public override bool Equals(object x, object y)
        {
            return ReferenceEquals(x, y);
        }
        public override int GetHashCode(object obj)
        {
            if (obj == null) return 0;
            return obj.GetHashCode();
        }
    }

    namespace ArrayExtensions
    {
        public static class ArrayExtensions
        {
            public static void ForEach(this Array array, Action<Array, int[]> action)
            {
                if (array.LongLength == 0) return;
                ArrayTraverse walker = new ArrayTraverse(array);
                do action(array, walker.Position);
                while (walker.Step());
            }
        }

        internal class ArrayTraverse
        {
            public int[] Position;
            private int[] maxLengths;

            public ArrayTraverse(Array array)
            {
                maxLengths = new int[array.Rank];
                for (int i = 0; i < array.Rank; ++i)
                {
                    maxLengths[i] = array.GetLength(i) - 1;
                }
                Position = new int[array.Rank];
            }

            public bool Step()
            {
                for (int i = 0; i < Position.Length; ++i)
                {
                    if (Position[i] < maxLengths[i])
                    {
                        Position[i]++;
                        for (int j = 0; j < i; j++)
                        {
                            Position[j] = 0;
                        }
                        return true;
                    }
                }
                return false;
            }
        }
    }

}