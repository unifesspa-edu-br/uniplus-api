namespace Unifesspa.UniPlus.Infrastructure.Core.Errors;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Traduz o código de um erro de domínio para a sua representação HTTP —
/// o trio <c>code → (status, type, title)</c> que a ADR-0024 define como fonte única
/// e que alimenta o catálogo público de erros.
/// </summary>
public interface IDomainErrorMapper
{
    bool TryGetMapping(string code, [MaybeNullWhen(false)] out DomainErrorMapping mapping);

    /// <summary>
    /// URI da página do catálogo público que explica <paramref name="code"/> — o campo
    /// <c>type</c> do corpo RFC 9457. Deriva do código, e não do registro, para que
    /// também o código de fallback (erro não mapeado) tenha destino.
    /// </summary>
    string GetProblemTypeUri(string code);
}
