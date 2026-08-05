namespace MeetingMinutesAI.Domain.Common;

public sealed class DomainRuleException : InvalidOperationException
{
    public DomainRuleException(string message)
        : base(message)
    {
    }
}
