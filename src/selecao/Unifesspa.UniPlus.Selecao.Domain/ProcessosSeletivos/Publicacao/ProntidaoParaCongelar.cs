namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A ordem única dos portões que uma configuração precisa passar para ser congelada. Publicar,
/// retificar de uma vez e fechar a sessão recusam na mesma sequência, e a primeira pendência
/// decide o código que o cliente vê.
/// </summary>
internal static class ProntidaoParaCongelar
{
    /// <summary>Os portões, nomeados na ordem em que são avaliados.</summary>
    internal sealed record Portoes(
        Func<DomainError?> IdentificadorLegivel,
        Func<DomainError?> Localidade,
        Func<DomainError?> AlgoritmoDeContagem,
        Func<DomainError?> CalendarioVigente,
        Func<DomainError?> Conformidade,
        Func<DomainError?> Cronograma,
        Func<DomainError?> TipoDeAtoUnico,
        Func<DomainError?> ResultadoSemPapel,
        Func<DomainError?> TipoQueCongelaDeclaradoComoResultado,
        Func<DomainError?> Cascata,
        Func<DomainError?> PreCanonicalizacao);

    /// <summary>A primeira pendência, ou nulo se todos passam. Para no primeiro portão que recusa.</summary>
    internal static DomainError? PrimeiraPendencia(Portoes portoes)
    {
        ArgumentNullException.ThrowIfNull(portoes);

        Func<DomainError?>[] emOrdem =
        [
            portoes.IdentificadorLegivel,
            portoes.Localidade,
            portoes.AlgoritmoDeContagem,
            portoes.CalendarioVigente,
            portoes.Conformidade,
            portoes.Cronograma,
            portoes.TipoDeAtoUnico,
            portoes.ResultadoSemPapel,
            portoes.TipoQueCongelaDeclaradoComoResultado,
            portoes.Cascata,
            portoes.PreCanonicalizacao,
        ];

        return emOrdem.Select(static portao => portao()).FirstOrDefault(static pendencia => pendencia is not null);
    }
}
