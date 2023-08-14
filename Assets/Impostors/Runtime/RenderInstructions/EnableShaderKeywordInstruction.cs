namespace Impostors.RenderInstructions
{
    public sealed class EnableShaderKeywordInstruction : IRenderInstruction
    {
        public readonly int KeywordId;

        public EnableShaderKeywordInstruction(int keywordId)
        {
            KeywordId = keywordId;
        }

        public void ApplyCommandBuffer(CommandBufferProxy bufferProxy)
        {
            bufferProxy.EnableShaderKeyword(KeywordId);
        }
    }
}