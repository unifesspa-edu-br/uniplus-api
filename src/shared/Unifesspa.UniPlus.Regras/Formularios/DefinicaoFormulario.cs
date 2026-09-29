namespace Unifesspa.UniPlus.Regras.Formularios;

using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Descrição de um formulário para avaliação: etapas ordenadas com os seus itens, termos e as
/// derivações por regra que o formulário usa (UNI-REQ-0144, UNI-REQ-0145). Não é entidade: o módulo
/// dono a monta a partir da configuração que ele já validou, e o avaliador trabalha só sobre ela
/// (ADR-0135).
/// </summary>
/// <remarks>
/// As invariantes que tornariam a avaliação ambígua são protegidas com
/// <see cref="ArgumentException"/>: um fato produzido por dois itens, um fato que é ao mesmo tempo
/// item e derivado, e código de etapa ou de termo repetido. A recusa com mensagem ao administrador é
/// do cadastro, antes de a descrição existir.
/// </remarks>
public sealed record DefinicaoFormulario
{
    public DefinicaoFormulario(
        IReadOnlyList<DefinicaoEtapa> etapas,
        IReadOnlyList<DefinicaoTermo> termos,
        IReadOnlyList<RegrasDerivacaoFato> derivacoes)
    {
        ArgumentNullException.ThrowIfNull(etapas);
        ArgumentNullException.ThrowIfNull(termos);
        ArgumentNullException.ThrowIfNull(derivacoes);

        GarantirUnicos(etapas.Select(static e => e.Codigo), "etapa");
        GarantirUnicos(termos.Select(static t => t.Codigo), "termo");
        GarantirUnicos(
            etapas.SelectMany(static e => e.Itens).Select(static i => i.FatoCodigo)
                .Concat(derivacoes.Select(static d => d.CodigoFato)),
            "fato produzido no formulário");

        Etapas = [.. etapas];
        Termos = [.. termos];
        Derivacoes = [.. derivacoes];
    }

    public IReadOnlyList<DefinicaoEtapa> Etapas { get; }

    public IReadOnlyList<DefinicaoTermo> Termos { get; }

    public IReadOnlyList<RegrasDerivacaoFato> Derivacoes { get; }

    private static void GarantirUnicos(IEnumerable<string> codigos, string oQue)
    {
        HashSet<string> vistos = new(StringComparer.Ordinal);
        foreach (string codigo in codigos)
        {
            if (!vistos.Add(codigo))
            {
                throw new ArgumentException($"O código '{codigo}' aparece mais de uma vez como {oQue}.");
            }
        }
    }
}

/// <summary>
/// Uma etapa do formulário, com os itens em ordem e a exibição condicional da etapa inteira. Uma
/// etapa oculta leva todos os seus itens a não aplicável.
/// </summary>
public sealed record DefinicaoEtapa
{
    public DefinicaoEtapa(string codigo, PredicadoDnf? exibicao, IReadOnlyList<DefinicaoItem> itens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentNullException.ThrowIfNull(itens);

        Codigo = codigo;
        Exibicao = exibicao;
        Itens = [.. itens];
    }

    public string Codigo { get; }

    /// <summary>A condição de exibição da etapa — nula quando a etapa sempre aparece.</summary>
    public PredicadoDnf? Exibicao { get; }

    public IReadOnlyList<DefinicaoItem> Itens { get; }
}

/// <summary>
/// Um item do formulário: o fato que ele produz e as regras do item — exibição, obrigatoriedade e
/// restrições de valor (UNI-REQ-0145).
/// </summary>
public sealed record DefinicaoItem
{
    public DefinicaoItem(
        string fatoCodigo,
        PredicadoDnf? exibicao,
        Obrigatoriedade obrigatoriedade,
        IReadOnlyList<RestricaoValor> restricoes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fatoCodigo);
        ArgumentNullException.ThrowIfNull(obrigatoriedade);
        ArgumentNullException.ThrowIfNull(restricoes);

        FatoCodigo = fatoCodigo;
        Exibicao = exibicao;
        Obrigatoriedade = obrigatoriedade;
        Restricoes = [.. restricoes];
    }

    public string FatoCodigo { get; }

    /// <summary>A condição de exibição do item — nula quando o item sempre aparece na etapa.</summary>
    public PredicadoDnf? Exibicao { get; }

    public Obrigatoriedade Obrigatoriedade { get; }

    public IReadOnlyList<RestricaoValor> Restricoes { get; }

    /// <summary>Os fatos citados pelas regras do próprio item, sem a exibição da etapa.</summary>
    public IReadOnlyCollection<string> FatosCitados =>
        [.. (Exibicao?.FatosCitados ?? [])
            .Concat(Obrigatoriedade.FatosCitados)
            .Concat(Restricoes.SelectMany(static r => r.FatosCitados))
            .Distinct(StringComparer.Ordinal)];
}

/// <summary>
/// Um termo de consentimento exigido no formulário, com exibição e obrigatoriedade condicionadas
/// (UNI-REQ-0086).
/// </summary>
public sealed record DefinicaoTermo
{
    public DefinicaoTermo(string codigo, PredicadoDnf? exibicao, Obrigatoriedade obrigatoriedade)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentNullException.ThrowIfNull(obrigatoriedade);

        Codigo = codigo;
        Exibicao = exibicao;
        Obrigatoriedade = obrigatoriedade;
    }

    public string Codigo { get; }

    /// <summary>A condição de exibição do termo — nula quando o termo sempre aparece.</summary>
    public PredicadoDnf? Exibicao { get; }

    public Obrigatoriedade Obrigatoriedade { get; }
}
