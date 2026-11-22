namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Os campos declarados para uma votação, completos ou não.</summary>
public sealed record DeclaracaoDaVotacao(int? Quorum, string? Criterio, string? Desempate, string? Encerramento, string? SemUnanimidade)
{
    internal CriterioDeVotacao? CriterioConferido() => Converter<CriterioDeVotacao>(Criterio);
    internal DesempateDaVotacao? DesempateConferido() => Converter<DesempateDaVotacao>(Desempate);
    internal EncerramentoDaVotacao? EncerramentoConferido() => Converter<EncerramentoDaVotacao>(Encerramento);
    internal ResultadoSemUnanimidade? SemUnanimidadeConferido() => Converter<ResultadoSemUnanimidade>(SemUnanimidade);

    internal bool DeclaraAlgo() => Quorum.HasValue || Criterio is not null || Desempate is not null
        || Encerramento is not null || SemUnanimidade is not null;

    private static T? Converter<T>(string? codigo) where T : struct, Enum =>
        CatalogoDe<T>.TentarObter(codigo, out T valor) ? valor : null;
}
