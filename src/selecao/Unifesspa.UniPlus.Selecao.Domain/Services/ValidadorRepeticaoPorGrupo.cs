namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using Entities;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A exigência repetida por entidade repete pelas ocorrências de um grupo repetível do formulário
/// do processo (ADR-0138, UNI-REQ-0069), nomeado pelo código. O código só significa alguma coisa
/// contra os grupos que estão ao lado dele: vive aqui porque a configuração congelada volta por um
/// caminho próprio, que monta o mesmo grafo sem passar pela escrita.
/// </summary>
public static class ValidadorRepeticaoPorGrupo
{
    /// <summary>A repetição nomeia grupo que o processo não tem.</summary>
    public const string GrupoInexistente = "NoExigencia.TipoEntidadeInvalido";

    /// <summary>A primeira repetição de <paramref name="nos"/> que nomeia grupo ausente de <paramref name="grupos"/>.</summary>
    public static DomainError? PrimeiraSemGrupo(IEnumerable<NoExigencia> nos, IEnumerable<GrupoColetado> grupos)
    {
        ArgumentNullException.ThrowIfNull(nos);
        return PrimeiroGrupoInexistente(nos.Select(static n => n.RepetePorEntidade).OfType<string>(), grupos);
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
