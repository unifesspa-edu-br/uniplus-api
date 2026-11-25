namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Globalization;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Descrição de um formulário para avaliação: páginas ordenadas com os seus itens, termos, as
/// derivações por regra que o formulário usa e os agregados sobre os grupos repetíveis dele
/// (UNI-REQ-0144, UNI-REQ-0145, UNI-REQ-0146). Não é entidade: o módulo
/// dono a monta a partir da configuração que ele já validou, e o avaliador trabalha só sobre ela
/// (ADR-0135).
/// </summary>
/// <remarks>
/// As invariantes que tornariam a avaliação ambígua são protegidas com
/// <see cref="ArgumentException"/>: um fato produzido por dois itens ou subitens, um fato que é ao
/// mesmo tempo item e derivado, um grupo com o código de um fato, um agregado sobre grupo que o
/// formulário não tem, e código de página ou de termo repetido. A recusa com mensagem ao administrador é
/// do cadastro, antes de a descrição existir.
/// </remarks>
public sealed record DefinicaoFormulario
{
    public DefinicaoFormulario(
        IReadOnlyList<DefinicaoPagina> paginas,
        IReadOnlyList<DefinicaoTermo> termos,
        IReadOnlyList<DerivacaoDoFato> derivacoes,
        IReadOnlyList<DefinicaoAgregado>? agregados = null)
    {
        ArgumentNullException.ThrowIfNull(paginas);
        ArgumentNullException.ThrowIfNull(termos);
        ArgumentNullException.ThrowIfNull(derivacoes);
        IReadOnlyList<DefinicaoAgregado> sobreGrupos = agregados ?? [];

        if (Violacao(paginas, termos, derivacoes, sobreGrupos) is { } violacao)
        {
            throw new ArgumentException(violacao, nameof(paginas));
        }

        Paginas = [.. paginas];
        Termos = [.. termos];
        Derivacoes = [.. derivacoes];
        Agregados = [.. sobreGrupos];
    }

    public IReadOnlyList<DefinicaoPagina> Paginas { get; }

    public IReadOnlyList<DefinicaoTermo> Termos { get; }

    public IReadOnlyList<DerivacaoDoFato> Derivacoes { get; }

    /// <summary>Os fatos agregados sobre os grupos repetíveis, citáveis fora do grupo (ADR-0138).</summary>
    public IReadOnlyList<DefinicaoAgregado> Agregados { get; }

    internal List<(DefinicaoPagina Pagina, DefinicaoItem Item)> ItensDasPaginas() =>
        [.. Paginas.SelectMany(static pagina => pagina.Itens.Select(item => (pagina, item)))];

    internal List<(DefinicaoPagina Pagina, DefinicaoGrupo Grupo)> GruposDasPaginas() =>
        [.. Paginas.SelectMany(static pagina => pagina.Grupos.Select(grupo => (pagina, grupo)))];

    internal List<NoDoGrafo> NosDoGrafoDeFatos()
    {
        List<(DefinicaoPagina Pagina, DefinicaoItem Item)> itens = ItensDasPaginas();
        return [.. itens.Select(static (par, posicao) => new NoDoGrafo(
                    par.Item.FatoCodigo, [.. (par.Pagina.Exibicao?.FatosCitados ?? []).Concat(par.Item.FatosCitados)], posicao)),
                .. GruposDasPaginas().Select((par, posicao) => new NoDoGrafo(
                    par.Grupo.Codigo, [.. (par.Pagina.Exibicao?.FatosCitados ?? []).Concat(par.Grupo.FatosDoCandidatoCitados)], itens.Count + posicao)),
                .. Derivacoes.Select(static derivacao => new NoDoGrafo(derivacao.CodigoFato, derivacao.DependenciasDeclaradas, -1)),
                .. Agregados.Select(static agregado => new NoDoGrafo(agregado.Codigo, [agregado.GrupoCodigo], -1))];
    }

    /// <summary>
    /// A primeira invariante que a descrição viola, em texto para o administrador; nula quando não viola.
    /// Quem monta a definição a partir de conteúdo de fora confere aqui antes de construir.
    /// </summary>
    internal static string? Violacao(
        IReadOnlyList<DefinicaoPagina> paginas,
        IReadOnlyList<DefinicaoTermo> termos,
        IReadOnlyList<DerivacaoDoFato> derivacoes,
        IReadOnlyList<DefinicaoAgregado> sobreGrupos)
    {
        return Repetido(paginas.Select(static e => e.Codigo), "página")
            ?? Repetido(termos.Select(static t => t.Codigo), "termo")
            ?? Repetido(
                paginas.SelectMany(static e => e.Itens).Select(static i => i.FatoCodigo)
                    .Concat(paginas.SelectMany(static e => e.Grupos).SelectMany(static g => g.Subitens).Select(static i => i.FatoCodigo))
                    .Concat(derivacoes.Select(static d => d.CodigoFato))
                    .Concat(sobreGrupos.Select(static a => a.Codigo))
                    .Concat(paginas.SelectMany(static e => e.Grupos).Select(static g => g.Codigo)),
                "fato produzido ou grupo do formulário")
            ?? AgregadoSemGrupo(paginas, sobreGrupos);
    }

    private static string? AgregadoSemGrupo(IReadOnlyList<DefinicaoPagina> paginas, IReadOnlyList<DefinicaoAgregado> sobreGrupos)
    {
        HashSet<string> codigosDosGrupos = new(paginas.SelectMany(static e => e.Grupos).Select(static g => g.Codigo), StringComparer.Ordinal);
        return sobreGrupos.FirstOrDefault(a => !codigosDosGrupos.Contains(a.GrupoCodigo)) is { } semGrupo
            ? $"O agregado '{semGrupo.Codigo}' é sobre o grupo '{semGrupo.GrupoCodigo}', que o formulário não tem."
            : null;
    }

    private static string? Repetido(IEnumerable<string> codigos, string oQue)
    {
        HashSet<string> vistos = new(StringComparer.Ordinal);
        return codigos.FirstOrDefault(codigo => !vistos.Add(codigo)) is { } repetido
            ? $"O código '{repetido}' aparece mais de uma vez como {oQue}."
            : null;
    }
}

/// <summary>
/// Uma página do formulário, com os itens em ordem e a exibição condicional da página inteira. Uma
/// página oculta leva todos os seus itens a não aplicável.
/// </summary>
public sealed record DefinicaoPagina
{
    public DefinicaoPagina(
        string codigo, PredicadoDnf? exibicao, IReadOnlyList<DefinicaoItem> itens, IReadOnlyList<DefinicaoGrupo>? grupos = null)
    {
        if (Violacao(codigo) is { } violacao)
        {
            throw new ArgumentException(violacao, nameof(codigo));
        }

        ArgumentNullException.ThrowIfNull(itens);

        Codigo = codigo;
        Exibicao = exibicao;
        Itens = [.. itens];
        Grupos = [.. grupos ?? []];
    }

    internal static string? Violacao(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo) ? "Uma página do formulário não tem código." : null;

    public string Codigo { get; }

    /// <summary>A condição de exibição da página — nula quando a página sempre aparece.</summary>
    public PredicadoDnf? Exibicao { get; }

    public IReadOnlyList<DefinicaoItem> Itens { get; }

    /// <summary>Os grupos repetíveis da página, cada um uma lista de ocorrências (UNI-REQ-0146).</summary>
    public IReadOnlyList<DefinicaoGrupo> Grupos { get; }
}

/// <summary>
/// Um grupo repetível: uma lista de ocorrências, cada uma com os subitens — fatos de membro —, com
/// exibição e obrigatoriedade do grupo, o mínimo e, quando declarado, o máximo de ocorrências
/// (ADR-0138, UNI-REQ-0146), e se o próprio candidato é um dos membros.
/// As regras dos subitens citam fatos do candidato ou subitens anteriores da mesma ocorrência.
/// </summary>
public sealed record DefinicaoGrupo
{
    public DefinicaoGrupo(
        string codigo,
        PredicadoDnf? exibicao,
        Obrigatoriedade obrigatoriedade,
        int minimo,
        int? maximo,
        IReadOnlyList<DefinicaoItem> subitens,
        bool incluiCandidato)
    {
        if (Violacao(codigo, minimo, maximo) is { } violacao)
        {
            throw new ArgumentException(violacao, nameof(codigo));
        }
        ArgumentNullException.ThrowIfNull(obrigatoriedade);
        ArgumentNullException.ThrowIfNull(subitens);
        Codigo = codigo;
        Exibicao = exibicao;
        Obrigatoriedade = obrigatoriedade;
        Minimo = minimo;
        Maximo = maximo;
        Subitens = [.. subitens];
        IncluiCandidato = incluiCandidato;
    }

    /// <summary>A recusa do grupo sem código ou com limites de ocorrências incoerentes: mínimo negativo, ou máximo que não comporta o mínimo nem uma ocorrência.</summary>
    internal static string? Violacao(string? codigo, int minimo, int? maximo) =>
        string.IsNullOrWhiteSpace(codigo) ? "Um grupo do formulário não tem código."
        : minimo < 0 || (maximo is { } teto && teto < Math.Max(minimo, 1))
            ? $"Os limites de ocorrências do grupo '{codigo}' ({minimo}, {(maximo is { } m ? m.ToString(CultureInfo.InvariantCulture) : "sem máximo")}) são incoerentes."
            : null;

    public string Codigo { get; }

    /// <summary>A condição de exibição do grupo inteiro — nula quando o grupo sempre aparece na página.</summary>
    public PredicadoDnf? Exibicao { get; }

    public Obrigatoriedade Obrigatoriedade { get; }
    public int Minimo { get; }

    /// <summary>O máximo de ocorrências; nulo quando o grupo aceita qualquer quantidade.</summary>
    public int? Maximo { get; }
    public IReadOnlyList<DefinicaoItem> Subitens { get; }

    /// <summary>O próprio candidato é um dos membros: a lista tem exatamente uma ocorrência dele.</summary>
    public bool IncluiCandidato { get; }

    internal bool ContagemValida(int quantidade, Ternario obrigatorio) =>
        (Maximo is not { } maximo || quantidade <= maximo)
        && (quantidade >= Minimo || (quantidade == 0 && obrigatorio == Ternario.Falso));

    internal bool OcorrenciaDoCandidatoValida(IReadOnlyList<OcorrenciaRespondida> respondidas) =>
        !IncluiCandidato || respondidas.Count == 0 || CandidatoComoMembro.TemUmaOcorrenciaDoCandidato(respondidas);

    internal static EstadoFato EstadoDaLista(IReadOnlyList<AvaliacaoOcorrencia> ocorrencias, Ternario obrigatorio) =>
        ocorrencias.Count == 0 ? obrigatorio switch
        {
            Ternario.Falso => EstadoFato.NaoInformado,
            Ternario.Verdadeiro => EstadoFato.Resolvido,
            _ => EstadoFato.Indeterminado,
        } : ocorrencias.All(static ocorrencia => ocorrencia.Estado == EstadoFato.Resolvido)
            ? EstadoFato.Resolvido : EstadoFato.Indeterminado;

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
/// Um fato agregado sobre um grupo repetível: o fato de membro que ele agrega nas ocorrências do
/// grupo e a operação (UNI-REQ-0146, ADR-0138).
/// </summary>
public sealed record DefinicaoAgregado
{
    public DefinicaoAgregado(string codigo, string grupoCodigo, string fatoDeMembro, OperacaoAgregado operacao)
    {
        if (Violacao(codigo, grupoCodigo, fatoDeMembro) is { } violacao)
        {
            throw new ArgumentException(violacao, nameof(codigo));
        }

        if (!Enum.IsDefined(operacao))
        {
            throw new ArgumentOutOfRangeException(nameof(operacao), operacao, "A operação do agregado não é conhecida.");
        }

        Codigo = codigo;
        GrupoCodigo = grupoCodigo;
        FatoDeMembro = fatoDeMembro;
        Operacao = operacao;
    }

    internal static string? Violacao(string? codigo, string? grupoCodigo, string? fatoDeMembro) =>
        string.IsNullOrWhiteSpace(codigo) ? "Um agregado do formulário não tem código."
        : string.IsNullOrWhiteSpace(grupoCodigo) ? "Um agregado do formulário não diz o grupo que agrega."
        : string.IsNullOrWhiteSpace(fatoDeMembro) ? "Um agregado do formulário não diz o fato de membro que agrega."
        : null;

    public string Codigo { get; }

    public string GrupoCodigo { get; }

    public string FatoDeMembro { get; }

    public OperacaoAgregado Operacao { get; }
}

/// <summary>
/// Um termo de consentimento exigido no formulário, com exibição e obrigatoriedade condicionadas
/// (UNI-REQ-0086).
/// </summary>
public sealed record DefinicaoTermo
{
    public DefinicaoTermo(string codigo, PredicadoDnf? exibicao, Obrigatoriedade obrigatoriedade)
    {
        if (Violacao(codigo) is { } violacao)
        {
            throw new ArgumentException(violacao, nameof(codigo));
        }

        ArgumentNullException.ThrowIfNull(obrigatoriedade);

        Codigo = codigo;
        Exibicao = exibicao;
        Obrigatoriedade = obrigatoriedade;
    }

    internal static string? Violacao(string? codigo) =>
        string.IsNullOrWhiteSpace(codigo) ? "Um termo do formulário não tem código." : null;

    public string Codigo { get; }

    /// <summary>A condição de exibição do termo — nula quando o termo sempre aparece.</summary>
    public PredicadoDnf? Exibicao { get; }

    public Obrigatoriedade Obrigatoriedade { get; }
}
