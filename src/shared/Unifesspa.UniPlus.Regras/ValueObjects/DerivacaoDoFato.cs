namespace Unifesspa.UniPlus.Regras.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A regra de derivação completa de um fato: a lista de regras <c>{quando, contribui}</c> mais a
/// lista <b>explícita</b> de fatos dependentes. É o dado de estrutura fechada que o
/// binding <c>REGRA_DERIVACAO:</c> referencia.
/// </summary>
/// <remarks>
/// <para>
/// As dependências declaradas são <b>exatamente</b> a união dos fatos citados nos predicados das
/// regras — nem mais, nem menos. Uma dependência a mais bloquearia o motor por um fato que nenhuma
/// regra usa; uma a menos deixaria de fazer o motor esperar por um fato que uma regra de fato cita.
/// Por isso a factory recusa os dois desvios.
/// </para>
/// <para>
/// Todo código contribuído tem de pertencer ao domínio do fato, informado no cadastro. Um código
/// desconhecido é recusado aqui — não há alias, e um código fora do domínio congelado daquela
/// configuração nunca é traduzido.
/// </para>
/// </remarks>
public sealed record DerivacaoDoFato
{
    private readonly HashSet<string> _dependencias;

    private DerivacaoDoFato(string codigoFato, IReadOnlyList<RegraDerivacao> regras, HashSet<string> dependencias, bool booleano)
    {
        CodigoFato = codigoFato;
        Regras = regras;
        _dependencias = dependencias;
        Booleano = booleano;
    }

    /// <summary>
    /// O derivado é booleano: verdadeiro se alguma regra ativa, falso se nenhuma (ADR-0136). O
    /// categórico é a união dos códigos contribuídos.
    /// </summary>
    public bool Booleano { get; }

    /// <summary>Código do fato derivado que estas regras resolvem.</summary>
    public string CodigoFato { get; }

    public IReadOnlyList<RegraDerivacao> Regras { get; }

    /// <summary>União exata dos fatos citados pelas regras — a lista que o motor espera resolver.</summary>
    public IReadOnlyCollection<string> DependenciasDeclaradas => _dependencias;

    internal static string? Violacao(string? codigoFato) =>
        string.IsNullOrWhiteSpace(codigoFato) ? "Uma derivação do formulário não tem o código do fato." : null;

    /// <summary>
    /// Cria a regra de derivação de um fato validando a coerência estrutural.
    /// </summary>
    /// <param name="codigoFato">O fato derivado que estas regras resolvem.</param>
    /// <param name="regras">A lista de regras — pelo menos uma.</param>
    /// <param name="dependenciasDeclaradas">A lista explícita de fatos dependentes.</param>
    /// <param name="dominioDoFato">
    /// Os códigos de valor válidos do domínio do fato; cada <see cref="RegraDerivacao.Contribui"/>
    /// precisa pertencer a ele.
    /// </param>
    public static Result<DerivacaoDoFato> Criar(
        string codigoFato,
        IReadOnlyList<RegraDerivacao> regras,
        IReadOnlyCollection<string> dependenciasDeclaradas,
        IReadOnlyCollection<string> dominioDoFato)
    {
        ArgumentNullException.ThrowIfNull(codigoFato);
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(dependenciasDeclaradas);
        ArgumentNullException.ThrowIfNull(dominioDoFato);
        ExigirCodigo(codigoFato);
        return RecusaSemRegras(codigoFato, regras)
            ?? RecusaForaDoDominio(codigoFato, regras, dominioDoFato)
            ?? CriarComEstrutura(codigoFato, regras, dependenciasDeclaradas, booleano: false);
    }

    /// <summary>
    /// Cria a derivação de um fato booleano: as regras não contribuem código, e a ativa torna o
    /// derivado verdadeiro.
    /// </summary>
    public static Result<DerivacaoDoFato> CriarBooleana(
        string codigoFato,
        IReadOnlyList<RegraDerivacao> regras,
        IReadOnlyCollection<string> dependenciasDeclaradas)
    {
        ArgumentNullException.ThrowIfNull(codigoFato);
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(dependenciasDeclaradas);
        ExigirCodigo(codigoFato);
        return RecusaSemRegras(codigoFato, regras)
            ?? RecusaContribuicaoNaBooleana(codigoFato, regras)
            ?? CriarComEstrutura(codigoFato, regras, dependenciasDeclaradas, booleano: true);
    }

    private static void ExigirCodigo(string codigoFato)
    {
        if (Violacao(codigoFato) is { } violacao)
        {
            throw new ArgumentException(violacao, nameof(codigoFato));
        }
    }

    private static Result<DerivacaoDoFato>? RecusaSemRegras(string codigoFato, IReadOnlyList<RegraDerivacao> regras) =>
        regras.Count == 0
            ? Falha(DerivacaoDoFatoErrorCodes.SemRegras, $"A derivação de '{codigoFato}' precisa de ao menos uma regra.")
            : null;

    private static Result<DerivacaoDoFato>? RecusaForaDoDominio(
        string codigoFato, IReadOnlyList<RegraDerivacao> regras, IReadOnlyCollection<string> dominioDoFato)
    {
        HashSet<string> dominio = new(dominioDoFato, StringComparer.Ordinal);
        return regras.FirstOrDefault(r => r.Contribui is null || !dominio.Contains(r.Contribui)) is { } fora
            ? Falha(
                DerivacaoDoFatoErrorCodes.ContribuiForaDoDominio,
                $"A regra que contribui '{fora.Contribui}' cita um código fora do domínio de '{codigoFato}'.")
            : null;
    }

    private static Result<DerivacaoDoFato>? RecusaContribuicaoNaBooleana(string codigoFato, IReadOnlyList<RegraDerivacao> regras) =>
        regras.Any(static r => r.Contribui is not null)
            ? Falha(
                DerivacaoDoFatoErrorCodes.ContribuiForaDoDominio,
                $"A derivação booleana de '{codigoFato}' não contribui código: a regra ativa torna o derivado verdadeiro.")
            : null;

    private static Result<DerivacaoDoFato> CriarComEstrutura(
        string codigoFato,
        IReadOnlyList<RegraDerivacao> regras,
        IReadOnlyCollection<string> dependenciasDeclaradas,
        bool booleano)
    {
        HashSet<string> citados = new(regras.SelectMany(static r => r.FatosCitados), StringComparer.Ordinal);
        HashSet<string> declarados = new(dependenciasDeclaradas, StringComparer.Ordinal);
        return RecusaAutorreferencia(codigoFato.Trim(), citados)
            ?? RecusaDependenciasIncoerentes(codigoFato, citados, declarados)
            ?? Result<DerivacaoDoFato>.Success(new DerivacaoDoFato(codigoFato.Trim(), [.. regras], declarados, booleano));
    }

    // Auto-referência: uma regra que cita o próprio fato derivado exigiria que ele já estivesse
    // resolvido para ser computado — o gate do motor o manteria indeterminado para sempre, ou o
    // decidiria por um valor pré-computado obsoleto. É recusada no cadastro.
    private static Result<DerivacaoDoFato>? RecusaAutorreferencia(string codigoNormalizado, HashSet<string> citados) =>
        citados.Contains(codigoNormalizado)
            ? Falha(
                DerivacaoDoFatoErrorCodes.DerivacaoAutorreferente,
                $"A derivação de '{codigoNormalizado}' cita o próprio fato — um derivado não pode depender de si mesmo.")
            : null;

    private static Result<DerivacaoDoFato>? RecusaDependenciasIncoerentes(string codigoFato, HashSet<string> citados, HashSet<string> declarados) =>
        citados.SetEquals(declarados)
            ? null
            : Falha(
                DerivacaoDoFatoErrorCodes.DependenciasIncoerentes,
                $"As dependências declaradas de '{codigoFato}' devem ser exatamente os fatos citados ({DetalheDasDependencias(citados, declarados)}).");

    private static string DetalheDasDependencias(HashSet<string> citados, HashSet<string> declarados)
    {
        IEnumerable<string> naoDeclarados = citados.Except(declarados, StringComparer.Ordinal);
        IEnumerable<string> naoCitados = declarados.Except(citados, StringComparer.Ordinal);
        return string.Join("; ", new[]
        {
            naoDeclarados.Any() ? $"citados mas não declarados: {string.Join(", ", naoDeclarados.Order(StringComparer.Ordinal))}" : null,
            naoCitados.Any() ? $"declarados mas não citados: {string.Join(", ", naoCitados.Order(StringComparer.Ordinal))}" : null,
        }.Where(static s => s is not null));
    }

    private static Result<DerivacaoDoFato> Falha(string codigo, string mensagem) =>
        Result<DerivacaoDoFato>.Failure(new DomainError(codigo, mensagem));
}

/// <summary>Códigos de erro de <see cref="DerivacaoDoFato"/>.</summary>
public static class DerivacaoDoFatoErrorCodes
{
    public const string SemRegras = "RegrasDerivacaoFato.SemRegras";
    public const string ContribuiForaDoDominio = "RegrasDerivacaoFato.ContribuiForaDoDominio";
    public const string DependenciasIncoerentes = "RegrasDerivacaoFato.DependenciasIncoerentes";
    public const string DerivacaoAutorreferente = "RegrasDerivacaoFato.DerivacaoAutorreferente";
}
