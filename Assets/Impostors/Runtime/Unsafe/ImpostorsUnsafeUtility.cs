using Impostors.Structs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Impostors.Unsafe
{
    public static class ImpostorsUnsafeUtility
    {
        public static unsafe void SetNativeImpostorArray(Impostor[] impostorArray,
            NativeArray<Impostor> impostorNativeArray, int length)
        {
            // pin the target vertex array and get a pointer to it
            fixed (void* impostorArrayPointer = impostorArray)
            {
                // memcopy the native array over the top
                UnsafeUtility.MemCpy(NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(impostorNativeArray),
                    impostorArrayPointer,
                    length * (long) UnsafeUtility.SizeOf<Impostor>());
            }
        }
    }
}