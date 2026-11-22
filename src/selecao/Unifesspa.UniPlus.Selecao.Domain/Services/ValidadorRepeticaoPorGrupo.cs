namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Entities;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Confere se os grupos de repetição das regras existem no formulário (ADR-0138, UNI-REQ-0069).
/// </summary>
public static class ValidadorRepeticaoPorGrupo
{
    /// <summary>A repetição nomeia grupo que o processo não tem.</summary>
    public const string GrupoInexistente = "RegraDeComprovacao.GrupoParaRepeticaoNaoDeclarado";

    /// <summary>A primeira repetição de <paramref name="regras"/> que nomeia grupo ausente de <paramref name="grupos"/>.</summary>
    public static DomainError? PrimeiraSemGrupo(IEnumerable<RegraDeComprovacao> regras, IEnumerable<GrupoColetado> grupos)
    {
        ArgumentNullException.ThrowIfNull(regras);
        return PrimeiroGrupoInexistente(regras.Select(static n => n.GrupoDoFormularioParaRepeticao).OfType<string>(), grupos);
    }

    /// <summary>O primeiro código de repetição ausente de <paramref name="grupos"/>.</summary>
    public static DomainError? PrimeiroGrupoInexistente(IEnumerable<string> codigos, IEnumerable<GrupoColetado> grupos)
    {
        ArgumentNullException.ThrowIfNull(codigos);
        ArgumentNullException.ThrowIfNull(grupos);

        HashSet<string> existentes = new(grupos.Select(static g => g.Codigo), StringComparer.Ordinal);
        return codigos.Select(codigo => RecusarGrupoInexistente(codigo, existentes)).OfType<DomainError>().FirstOrDefault();
    }

    /// <summary>Recusa a repetição que não corresponde a um grupo dos formulários.</summary>
    public static DomainError? RecusarGrupoInexistente(string? codigo, IReadOnlySet<string> existentes)
    {
        ArgumentNullException.ThrowIfNull(existentes);
        return codigo is not null && !existentes.Contains(codigo)
            ? new(GrupoInexistente, "O grupo de repetição informado não existe nos formulários do processo. Escolha um grupo existente ou retire a repetição.")
            : null;
    }
}
