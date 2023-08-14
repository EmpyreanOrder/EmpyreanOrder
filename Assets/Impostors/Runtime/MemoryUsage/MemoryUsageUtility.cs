using System.Collections.Generic;
using System.Runtime.InteropServices;
using Impostors.Unsafe;
using Unity.Collections;

namespace Impostors.MemoryUsage
{
    internal static class MemoryUsageUtility
    {
        private const int ReferenceSize = 8; // 8 bytes is size of pointer in x64 OS

        public static int GetMemoryUsage<T>(List<T> list)
        {
            if (typeof(T).IsValueType)
                return list.Capacity * Marshal.SizeOf<T>();
            else
                return list.Capacity * ReferenceSize;
        }

        public static int GetMemoryUsage<T>(T[] array)
        {
            if (typeof(T).IsValueType)
                return array.Length * Marshal.SizeOf<T>();
            else
                return array.Length * ReferenceSize;
        }

        public static int GetMemoryUsage<K,V>(Dictionary<K,V> dictionary)
        {
            int keySize = ReferenceSize;
            int valueSize = ReferenceSize;
            
            if (typeof(K).IsValueType)
                keySize = Marshal.SizeOf<K>();
            if (typeof(V).IsValueType)
                keySize = Marshal.SizeOf<V>();

            return dictionary.Count * (keySize + valueSize);
        }

        public static int GetMemoryUsage<T>(NativeList<T> list) where T : unmanaged
        {
            return list.Capacity * Marshal.SizeOf<T>();
        }

        public static int GetMemoryUsage<T>(NativeArray<T> array) where T : unmanaged
        {
            return array.Length * Marshal.SizeOf<T>();
        }

        public static int GetMemoryUsage<T>(NativeQueue<T> queue) where T : unmanaged
        {
            return queue.Count * Marshal.SizeOf<T>();
        }

        public static int GetMemoryUsage<T>(NativeStack<T> stack) where T : unmanaged
        {
            return stack.Capacity * Marshal.SizeOf<T>();
        }

        public static int ConvertBytesToMegabytes(int bytes)
        {
            return (bytes / 1024) / 1024;
        }

        public static float ConvertBytesToDecimalMegabytes(int bytes)
        {
            return bytes / 1024f / 1024f;
        }
    }
}