namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

public sealed record DomainErrorMapping(int Status, string Type, string Title);
