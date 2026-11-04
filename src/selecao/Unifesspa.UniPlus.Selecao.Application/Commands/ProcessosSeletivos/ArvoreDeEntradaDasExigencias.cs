namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// Perguntas à floresta de entrada das exigências, a qualquer profundidade e sem I/O: o que ela cita, para
/// decidir o que ler antes de construí-la.
/// </summary>
internal static class ArvoreDeEntradaDasExigencias
{
    /// <summary>Se o nó é uma folha, que carrega um documento, e não um grupo de exigências.</summary>
    public static bool EhFolha(NoExigenciaInput no) => string.Equals(no.Tipo, "FOLHA", StringComparison.Ordinal);

    /// <summary>Se alguma folha declara gatilho.</summary>
    public static bool ExisteGatilho(IReadOnlyList<NoExigenciaInput> nos) =>
        nos.Any(no => (no.Documento?.Condicoes.Count ?? 0) > 0 || ExisteGatilho(no.Filhos ?? []));

    /// <summary>Se alguma folha declara fato comprovado.</summary>
    public static bool ExisteFatoComprovado(IReadOnlyList<NoExigenciaInput> nos) =>
        nos.Any(no => (no.Documento?.FatosComprovados?.Count ?? 0) > 0 || ExisteFatoComprovado(no.Filhos ?? []));

    /// <summary>Os modelos de documento que as folhas citam.</summary>
    public static HashSet<Guid> ModelosCitados(IReadOnlyList<NoExigenciaInput> nos) =>
        [.. nos.SelectMany(static no => (no.Documento?.ModeloId is { } id ? [id] : Array.Empty<Guid>()).Concat(ModelosCitados(no.Filhos ?? [])))];

    /// <summary>Os grupos que a árvore nomeia na repetição por entidade.</summary>
    public static IEnumerable<string> GruposDaRepeticao(IEnumerable<NoExigenciaInput> nos) =>
        nos.SelectMany(static no => (no.RepetePorEntidade is { } grupo ? [grupo] : Array.Empty<string>())
            .Concat(GruposDaRepeticao(no.Filhos ?? [])));
}
