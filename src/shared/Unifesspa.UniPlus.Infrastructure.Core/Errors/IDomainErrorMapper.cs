namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Diagnostics.CodeAnalysis;

public interface IDomainErrorMapper
{
    bool TryGetMapping(string code, [MaybeNullWhen(false)] out DomainErrorMapping mapping);
}
