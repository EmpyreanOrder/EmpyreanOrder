namespace Impostors.RenderInstructions
{
    public interface IRenderInstruction
    {
        void ApplyCommandBuffer(CommandBufferProxy bufferProxy);
    }
}