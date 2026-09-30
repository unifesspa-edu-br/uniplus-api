namespace Unifesspa.UniPlus.Regras.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A regra de derivação completa de um fato: a lista de regras <c>{quando, contribui}</c> mais a
/// lista <b>explícita</b> de fatos dependentes (Story #927). É o dado de estrutura fechada que o
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
public sealed record RegrasDerivacaoFato
{
    private readonly HashSet<string> _dependencias;

    private RegrasDerivacaoFato(string codigoFato, IReadOnlyList<RegraDerivacao> regras, HashSet<string> dependencias, bool booleano)
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
    public static Result<RegrasDerivacaoFato> Criar(
        string codigoFato,
        IReadOnlyList<RegraDerivacao> regras,
        IReadOnlyCollection<string> dependenciasDeclaradas,
        IReadOnlyCollection<string> dominioDoFato)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoFato);
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(dependenciasDeclaradas);
        ArgumentNullException.ThrowIfNull(dominioDoFato);

        if (regras.Count == 0)
        {
            return Result<RegrasDerivacaoFato>.Failure(new DomainError(
                RegrasDerivacaoFatoErrorCodes.SemRegras,
                $"A derivação de '{codigoFato}' precisa de ao menos uma regra."));
        }

        HashSet<string> dominio = new(dominioDoFato, StringComparer.Ordinal);
        foreach (RegraDerivacao regra in regras)
        {
            if (regra.Contribui is null || !dominio.Contains(regra.Contribui))
            {
                return Result<RegrasDerivacaoFato>.Failure(new DomainError(
                    RegrasDerivacaoFatoErrorCodes.ContribuiForaDoDominio,
                    $"A regra que contribui '{regra.Contribui}' cita um código fora do domínio de '{codigoFato}'."));
            }
        }

        return CriarComEstrutura(codigoFato, regras, dependenciasDeclaradas, booleano: false);
    }

    /// <summary>
    /// Cria a derivação de um fato booleano: as regras não contribuem código, e a ativa torna o
    /// derivado verdadeiro.
    /// </summary>
    public static Result<RegrasDerivacaoFato> CriarBooleana(
        string codigoFato,
        IReadOnlyList<RegraDerivacao> regras,
        IReadOnlyCollection<string> dependenciasDeclaradas)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigoFato);
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(dependenciasDeclaradas);

        if (regras.Count == 0)
        {
            return Result<RegrasDerivacaoFato>.Failure(new DomainError(
                RegrasDerivacaoFatoErrorCodes.SemRegras,
                $"A derivação de '{codigoFato}' precisa de ao menos uma regra."));
        }

        if (regras.Any(static r => r.Contribui is not null))
        {
            return Result<RegrasDerivacaoFato>.Failure(new DomainError(
                RegrasDerivacaoFatoErrorCodes.ContribuiForaDoDominio,
                $"A derivação booleana de '{codigoFato}' não contribui código: a regra ativa torna o derivado verdadeiro."));
        }

        return CriarComEstrutura(codigoFato, regras, dependenciasDeclaradas, booleano: true);
    }

    private static Result<RegrasDerivacaoFato> CriarComEstrutura(
        string codigoFato,
        IReadOnlyList<RegraDerivacao> regras,
        IReadOnlyCollection<string> dependenciasDeclaradas,
        bool booleano)
    {
        HashSet<string> citados = new(regras.SelectMany(static r => r.FatosCitados), StringComparer.Ordinal);

        // Auto-referência: uma regra que cita o próprio fato derivado exigiria que ele já estivesse
        // resolvido para ser computado — o gate do motor o manteria indeterminado para sempre, ou o
        // decidiria por um valor pré-computado obsoleto. É recusada no cadastro.
        string codigoNormalizado = codigoFato.Trim();
        if (citados.Contains(codigoNormalizado))
        {
            return Result<RegrasDerivacaoFato>.Failure(new DomainError(
                RegrasDerivacaoFatoErrorCodes.DerivacaoAutorreferente,
                $"A derivação de '{codigoNormalizado}' cita o próprio fato — um derivado não pode depender de si mesmo."));
        }

        HashSet<string> declarados = new(dependenciasDeclaradas, StringComparer.Ordinal);

        if (!citados.SetEquals(declarados))
        {
            IEnumerable<string> naoDeclarados = citados.Except(declarados, StringComparer.Ordinal);
            IEnumerable<string> naoCitados = declarados.Except(citados, StringComparer.Ordinal);
            string detalhe = string.Join("; ", new[]
            {
                naoDeclarados.Any() ? $"citados mas não declarados: {string.Join(", ", naoDeclarados.Order(StringComparer.Ordinal))}" : null,
                naoCitados.Any() ? $"declarados mas não citados: {string.Join(", ", naoCitados.Order(StringComparer.Ordinal))}" : null,
            }.Where(static s => s is not null));

            return Result<RegrasDerivacaoFato>.Failure(new DomainError(
                RegrasDerivacaoFatoErrorCodes.DependenciasIncoerentes,
                $"As dependências declaradas de '{codigoFato}' devem ser exatamente os fatos citados ({detalhe})."));
        }

        return Result<RegrasDerivacaoFato>.Success(
            new RegrasDerivacaoFato(codigoFato.Trim(), [.. regras], declarados, booleano));
    }
}

/// <summary>Códigos de erro de <see cref="RegrasDerivacaoFato"/>.</summary>
public static class RegrasDerivacaoFatoErrorCodes
{
    public const string SemRegras = "RegrasDerivacaoFato.SemRegras";
    public const string ContribuiForaDoDominio = "RegrasDerivacaoFato.ContribuiForaDoDominio";
    public const string DependenciasIncoerentes = "RegrasDerivacaoFato.DependenciasIncoerentes";
    public const string DerivacaoAutorreferente = "RegrasDerivacaoFato.DerivacaoAutorreferente";
}
