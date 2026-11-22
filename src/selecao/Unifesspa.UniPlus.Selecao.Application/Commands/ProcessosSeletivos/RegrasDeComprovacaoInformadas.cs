namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// Consulta as regras informadas em qualquer nível, sem I/O, para decidir quais dados ler antes da montagem.
/// </summary>
internal static class RegrasDeComprovacaoInformadas
{
    /// <summary>Se a regra informada descreve um documento.</summary>
    public static bool EhRegraDeDocumento(RegraDeComprovacaoInformada regra) => CatalogoDe<FormaDeComprovacao>.TentarObter(regra.Forma, out FormaDeComprovacao forma) && forma == FormaDeComprovacao.Documento;

    /// <summary>Se alguma regra de documento declara gatilho.</summary>
    public static bool ExisteGatilho(IReadOnlyList<RegraDeComprovacaoInformada> regras) =>
        regras.Any(regra => (regra.Documento?.Condicoes().Count ?? 0) > 0 || ExisteGatilho(regra.Itens ?? []));

    /// <summary>Se alguma regra de documento declara fato comprovado.</summary>
    public static bool ExisteFatoComprovado(IReadOnlyList<RegraDeComprovacaoInformada> regras) =>
        regras.Any(regra => (regra.Documento?.FatosComprovados?.Count ?? 0) > 0 || ExisteFatoComprovado(regra.Itens ?? []));

    /// <summary>Os modelos citados pelas regras de documento.</summary>
    public static HashSet<Guid> ModelosCitados(IReadOnlyList<RegraDeComprovacaoInformada> regras) =>
        [.. regras.SelectMany(static regra => (regra.Documento?.ModeloId() is { } id ? [id] : Array.Empty<Guid>()).Concat(ModelosCitados(regra.Itens ?? [])))];

}
