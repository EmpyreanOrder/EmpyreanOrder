namespace Impostors.RenderInstructions
{
    public sealed class DisableShaderKeywordInstruction : IRenderInstruction
    {
        public readonly int KeywordId;

        public DisableShaderKeywordInstruction(int keywordId)
        {
            KeywordId = keywordId;
        }

        public void ApplyCommandBuffer(CommandBufferProxy bufferProxy)
        {
            bufferProxy.DisableShaderKeyword(KeywordId);
        }
    }
}