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
/// <see cref="ArgumentException"/>: um fato produzido por dois itens ou subitens, um fato que é ao
/// mesmo tempo item e derivado, um grupo com o código de um fato, e código de etapa ou de termo
/// repetido. A recusa com mensagem ao administrador é
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
                .Concat(etapas.SelectMany(static e => e.Grupos).SelectMany(static g => g.Subitens).Select(static i => i.FatoCodigo))
                .Concat(derivacoes.Select(static d => d.CodigoFato))
                .Concat(etapas.SelectMany(static e => e.Grupos).Select(static g => g.Codigo)),
            "fato produzido ou grupo do formulário");

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
    public DefinicaoEtapa(
        string codigo, PredicadoDnf? exibicao, IReadOnlyList<DefinicaoItem> itens, IReadOnlyList<DefinicaoGrupo>? grupos = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentNullException.ThrowIfNull(itens);

        Codigo = codigo;
        Exibicao = exibicao;
        Itens = [.. itens];
        Grupos = [.. grupos ?? []];
    }

    public string Codigo { get; }

    /// <summary>A condição de exibição da etapa — nula quando a etapa sempre aparece.</summary>
    public PredicadoDnf? Exibicao { get; }

    public IReadOnlyList<DefinicaoItem> Itens { get; }

    /// <summary>Os grupos repetíveis da etapa, cada um uma lista de ocorrências (UNI-REQ-0146).</summary>
    public IReadOnlyList<DefinicaoGrupo> Grupos { get; }
}

/// <summary>
/// Um grupo repetível: uma lista de ocorrências, cada uma com os subitens — fatos de membro —, com
/// exibição e obrigatoriedade do grupo e o mínimo e o máximo de ocorrências (ADR-0138, UNI-REQ-0146).
/// As regras dos subitens citam fatos do candidato ou subitens anteriores da mesma ocorrência.
/// </summary>
public sealed record DefinicaoGrupo
{
    public DefinicaoGrupo(
        string codigo,
        PredicadoDnf? exibicao,
        Obrigatoriedade obrigatoriedade,
        int minimo,
        int maximo,
        IReadOnlyList<DefinicaoItem> subitens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentNullException.ThrowIfNull(obrigatoriedade);
        ArgumentNullException.ThrowIfNull(subitens);
        ArgumentOutOfRangeException.ThrowIfNegative(minimo);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximo, Math.Max(minimo, 1));

        Codigo = codigo;
        Exibicao = exibicao;
        Obrigatoriedade = obrigatoriedade;
        Minimo = minimo;
        Maximo = maximo;
        Subitens = [.. subitens];
    }

    public string Codigo { get; }

    /// <summary>A condição de exibição do grupo inteiro — nula quando o grupo sempre aparece na etapa.</summary>
    public PredicadoDnf? Exibicao { get; }

    public Obrigatoriedade Obrigatoriedade { get; }
    public int Minimo { get; }
    public int Maximo { get; }
    public IReadOnlyList<DefinicaoItem> Subitens { get; }

    /// <summary>
    /// Os fatos do candidato de que o grupo depende: os da exibição e da obrigatoriedade dele e os
    /// que os subitens citam fora da ocorrência.
    /// </summary>
    public IReadOnlyCollection<string> FatosDoCandidatoCitados
    {
        get
        {
            HashSet<string> dosSubitens = new(Subitens.Select(static s => s.FatoCodigo), StringComparer.Ordinal);
            return [.. (Exibicao?.FatosCitados ?? [])
                .Concat(Obrigatoriedade.FatosCitados)
                .Concat(Subitens.SelectMany(static s => s.FatosCitados).Where(f => !dosSubitens.Contains(f)))
                .Distinct(StringComparer.Ordinal)];
        }
    }
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
