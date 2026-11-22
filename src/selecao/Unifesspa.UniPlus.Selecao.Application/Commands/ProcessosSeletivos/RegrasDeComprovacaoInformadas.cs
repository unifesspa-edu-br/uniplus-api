namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Consulta as regras informadas em qualquer nível, sem I/O, para decidir quais dados ler antes da montagem.
/// </summary>
internal static class RegrasDeComprovacaoInformadas
{
    /// <summary>Se a regra informada descreve um documento.</summary>
    public static bool EhFolha(NoExigenciaInput no) => string.Equals(no.Tipo, "FOLHA", StringComparison.Ordinal);

    /// <summary>Se alguma regra de documento declara gatilho.</summary>
    public static bool ExisteGatilho(IReadOnlyList<NoExigenciaInput> nos) =>
        nos.Any(no => (no.Documento?.Condicoes().Count ?? 0) > 0 || ExisteGatilho(no.Filhos ?? []));

    /// <summary>Se alguma regra de documento declara fato comprovado.</summary>
    public static bool ExisteFatoComprovado(IReadOnlyList<NoExigenciaInput> nos) =>
        nos.Any(no => (no.Documento?.FatosComprovados?.Count ?? 0) > 0 || ExisteFatoComprovado(no.Filhos ?? []));

    /// <summary>Os modelos citados pelas regras de documento.</summary>
    public static HashSet<Guid> ModelosCitados(IReadOnlyList<NoExigenciaInput> nos) =>
        [.. nos.SelectMany(static no => (no.Documento?.ModeloId() is { } id ? [id] : Array.Empty<Guid>()).Concat(ModelosCitados(no.Filhos ?? [])))];

}
