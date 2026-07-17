namespace RagChatbotSystem.Business.Exceptions
{
    public enum ChatBlockReason
    {
        DailyTokenLimit,
        InsufficientCredits
    }

    public sealed class ChatRequestBlockedException : InvalidOperationException
    {
        public ChatRequestBlockedException(ChatBlockReason reason, string message)
            : base(message)
        {
            Reason = reason;
        }

        public ChatBlockReason Reason { get; }
    }
}
