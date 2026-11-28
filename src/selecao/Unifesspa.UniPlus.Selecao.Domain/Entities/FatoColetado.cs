namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Um fato do vocabulário que <b>este</b> processo seletivo coleta do candidato, com a sua
/// posição na ordem de coleta, a pré-condição que decide se o campo produtor é apresentado
/// e a apresentação do campo no formulário de inscrição — rótulo, tipo de
/// tipo de campo e obrigatoriedade.
/// </summary>
/// <remarks>
/// <para>
/// A pré-condição é <b>aresta do grafo por processo, não propriedade do catálogo</b>: o mesmo
/// fato pode ser coletado sem gate nenhum em outro processo. Por isso vive aqui, na configuração
/// do certame, e não em <c>FatoCandidato</c> — que é catálogo global e seed-governado (ADR-0111).
/// A apresentação é pelo mesmo motivo: o mesmo fato pode ter rótulo e obrigatoriedade diferentes
/// em processos diferentes.
/// </para>
/// <para>
/// Fato <b>sem</b> pré-condição é coletado sempre — é o caso das autodeclarações de abertura, que
/// não dependem de nada anterior. Fato com pré-condição só é apresentado quando o predicado
/// resolve verdadeiro; quando resolve falso, o fato vinculado passa a não-aplicável, e quando
/// fica indeterminado, o fato o acompanha.
/// </para>
/// </remarks>
public sealed class FatoColetado : EntityBase, IParteDoProcesso
{
    private readonly List<CondicaoPrecondicaoFato> _precondicoes = [];

    public Guid ProcessoSeletivoId { get; private set; }

    /// <summary>O grupo repetível de que o fato é campo; nulo no item do formulário.</summary>
    public Guid? GrupoColetadoId { get; private set; }

    /// <summary>Código do fato no vocabulário fechado do candidato.</summary>
    public string FatoCodigo { get; private set; } = string.Empty;

    /// <summary>
    /// Posição na ordem de coleta. Estritamente crescente e única no processo: é ela que dá
    /// sentido a "fato anterior", e todo fato citado numa pré-condição precisa ter ordem menor
    /// que a do fato que o cita.
    /// </summary>
    public int Ordem { get; private set; }

    /// <summary>Rótulo do campo no formulário de inscrição.</summary>
    public string Rotulo { get; private set; } = string.Empty;

    /// <summary>
    /// Tipo de campo: como a pessoa responde à pergunta. A coerência contra o <c>Dominio</c> do fato no catálogo (ex.:
    /// tipo do dado <c>SIM_OU_NAO</c> só aceita <see cref="TipoDeCampo.SimOuNao"/>) é validada na
    /// Application, que tem acesso ao vocabulário cross-módulo — o Domain não alcança o catálogo.
    /// </summary>
    public TipoDeCampo TipoDeCampo { get; private set; }

    /// <summary>
    /// Quando o candidato é obrigado a responder: sempre, nunca ou quando o predicado sobre fatos
    /// anteriores é verdadeiro (UNI-REQ-0145). É do item, não do fato: o mesmo fato pode ser
    /// obrigatório para um candidato e opcional para outro.
    /// </summary>
    public Obrigatoriedade Obrigatoriedade { get; private set; } = Obrigatoriedade.Nunca;

    /// <summary>Texto de apoio exibido junto do campo.</summary>
    public string? Ajuda { get; private set; }

    /// <summary>Se o formulário pede a resposta duas vezes para conferir a digitação.</summary>
    public bool PedirConfirmacao { get; private set; }

    /// <summary>
    /// As restrições sobre o valor respondido (UNI-REQ-0145), no máximo uma de cada tipo e na ordem
    /// do tipo: faixa no campo numérico, tamanho no de texto, opções no de seleção.
    /// </summary>
    public IReadOnlyList<RestricaoValor> Restricoes { get; private set; } = [];

    /// <summary>
    /// De onde vêm as opções do campo no processo (ADR-0136). Copiada da fonte dos valores do
    /// fato no catálogo quando a coleta é definida e recopiada ao publicar, enquanto nenhuma versão
    /// publicada coletou o fato (<see cref="CopiarDoCatalogo"/>); depois de congelada, a cópia não
    /// muda mais.
    /// </summary>
    public OrigemValoresColeta OrigemValores { get; private set; }

    /// <summary>
    /// O formato da resposta de texto (livre, CPF, e-mail…), copiado do fato no catálogo quando a
    /// coleta é definida; existe se, e só se, o campo é de texto.
    /// </summary>
    public string? Formato { get; private set; }

    /// <summary>
    /// Classificação de proteção de dados do fato (token canônico: PUBLICO, INTERNO, PESSOAL,
    /// IDENTIFICADOR, SENSIVEL), copiada do catálogo quando a coleta é definida e recopiada ao
    /// publicar, enquanto nenhuma versão publicada coletou o fato; reclassificar o fato no catálogo
    /// depois não muda o que já foi congelado (ADR-0136).
    /// </summary>
    public string ClassificacaoProtecao { get; private set; } = string.Empty;

    /// <summary>
    /// Hipótese legal de tratamento do fato (token canônico do vocabulário da Configuração, que este
    /// Domain não alcança, ADR-0097), copiada do catálogo junto da classificação e congelada com ela
    /// (ADR-0154).
    /// </summary>
    public string HipoteseLegal { get; private set; } = string.Empty;

    /// <summary>Para que o dado do fato é tratado; copiada do catálogo e congelada junto da classificação (ADR-0154).</summary>
    public string FinalidadeTratamento { get; private set; } = string.Empty;

    /// <summary>
    /// O que o dado significa, para quem analisa a inscrição: copiada do
    /// catálogo e congelada com a classificação. Não é a <see cref="Ajuda"/>, que orienta o candidato.
    /// </summary>
    public string Descricao { get; private set; } = string.Empty;

    /// <summary>
    /// Como a tela exibe o valor (token canônico: MOEDA, SITUACAO), copiado do catálogo e congelado com a
    /// descrição; <see langword="null"/> quando o fato não declara e vale o padrão que a consulta resolve.
    /// </summary>
    public string? ComponenteExibicao { get; private set; }

    /// <summary>Se as opções do campo são as que o processo declara.</summary>
    public bool OpcoesDoProcesso => OrigemValores == OrigemValoresColeta.OpcoesDoProcesso;

    public IReadOnlyCollection<CondicaoPrecondicaoFato> Precondicoes => _precondicoes.AsReadOnly();

    /// <summary>A resposta que impede a inscrição e a mensagem ao candidato; nulo quando nenhuma impede.</summary>
    public Impedimento? Impedimento { get; private set; }

    /// <summary>O formulário que produz o fato; atribuído pelo processo ao definir os itens.</summary>
    public Guid? FormularioId { get; private set; }

    /// <summary>A seção do formulário em que o item aparece.</summary>
    public string? PaginaCodigo { get; private set; }

    private FatoColetado() { }

    private FatoColetado(
        string fatoCodigo,
        int ordem,
        string? paginaCodigo,
        ApresentacaoDoCampo apresentacao,
        RegrasDoCampo regras,
        CopiaDoItemNoCatalogo copia,
        FormularioProcesso? formulario)
    {
        FatoCodigo = fatoCodigo ?? string.Empty;
        Ordem = ordem;
        PaginaCodigo = paginaCodigo;
        FormularioId = formulario?.Id;
        AdotarApresentacao(apresentacao.NaFormaGravada());
        AdotarRegras(regras.ComRestricoesNaOrdemDoTipo());
        CopiarDoCatalogo(copia);
    }

    /// <summary>
    /// Acumula toda violação independente em vez de retornar na primeira (ADR-0125) — a forma do
    /// item, a cópia do catálogo e a autorreferência de pré-condição não dependem umas das outras. A
    /// mensagem de autorreferência não ecoa o código do fato (ADR-0023).
    /// </summary>
    /// <param name="formulario">O formulário que produz o fato, quando já existe; o processo liga o item ao formulário que define.</param>
    /// <exception cref="ArgumentException">A cópia do catálogo vem sem hipótese legal, finalidade do tratamento ou descrição.</exception>
    public static Result<FatoColetado> Criar(
        string fatoCodigo,
        int? ordem,
        string? paginaCodigo,
        ApresentacaoDoCampo apresentacao,
        RegrasDoCampo regras,
        CopiaDoItemNoCatalogo copia,
        FormularioProcesso? formulario = null)
    {
        ArgumentNullException.ThrowIfNull(apresentacao);
        ArgumentNullException.ThrowIfNull(regras);
        ExigirCopiaCompleta(copia);

        List<FieldError> erros = ConferirForma(fatoCodigo, ordem, ApresentacaoLidaDoCampo.De(apresentacao), RegrasLidasDoCampo.De(regras), copia);
        if (erros.Count > 0)
        {
            return Result<FatoColetado>.ValidationFailure(erros);
        }

        return Result<FatoColetado>.Success(new FatoColetado(fatoCodigo, ordem.GetValueOrDefault(), paginaCodigo, apresentacao, regras, copia, formulario));
    }

    /// <summary>
    /// Confere a cópia do catálogo contra a forma do item — o formato existe se, e só se, o campo é
    /// de texto, e cabe na coluna — e o vocabulário fechado da classificação, sem mudar o item.
    /// </summary>
    internal Result ConferirCopiaDoCatalogo(CopiaDoItemNoCatalogo copia)
    {
        ExigirCopiaCompleta(copia);

        // A mesma forma e o mesmo vocabulário que Criar confere.
        List<FieldError> erros = ConferirForma(FatoCodigo, Ordem, ApresentacaoLidaDoCampo.De(Apresentacao), RegrasLidasDoCampo.De(Regras), copia);
        return erros.Count > 0 ? Result.ValidationFailure(erros) : Result.Success();
    }

    private static void ExigirCopiaCompleta(CopiaDoItemNoCatalogo copia)
    {
        ArgumentNullException.ThrowIfNull(copia);
        if (copia.Violacao() is { } violacao)
        {
            throw new ArgumentException(violacao, nameof(copia));
        }
    }

    /// <summary>
    /// A forma do item e o vocabulário fechado da cópia do catálogo, uma só para a criação, para a cópia refeita e para
    /// quem lê o item com parte das regras recusada: a parte recusada vem nula em <paramref name="regras"/> e não é
    /// conferida aqui.
    /// </summary>
    public static List<FieldError> ConferirForma(
        string fatoCodigo, int? ordem, ApresentacaoLidaDoCampo apresentacao, RegrasLidasDoCampo regras, CopiaDoItemNoCatalogo copia)
    {
        ArgumentNullException.ThrowIfNull(apresentacao);
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(copia);

        return [.. FormaDoItem.Conferir(fatoCodigo, ordem, apresentacao, copia.Formato, regras), .. copia.ConferirVocabulario()];
    }

    private void AdotarApresentacao(ApresentacaoDoCampo apresentacao)
    {
        Rotulo = apresentacao.Rotulo;
        TipoDeCampo = apresentacao.TipoDeCampo;
        Ajuda = apresentacao.Ajuda;
        PedirConfirmacao = apresentacao.PedirConfirmacao;
    }

    private void AdotarRegras(RegrasDoCampo regras)
    {
        Obrigatoriedade = regras.Obrigatoriedade;
        Restricoes = regras.Restricoes;
        Impedimento = regras.Impedimento;
        _precondicoes.AddRange(CondicaoPrecondicaoFato.DaExibicao(regras.Exibicao, Id));
    }

    /// <summary>
    /// Refaz as cópias do item tiradas do catálogo — origem dos valores, formato, proteção
    /// (classificação, hipótese legal e finalidade), descrição e componente de exibição, na forma em
    /// que o envelope as congela. O rótulo, o tipo de campo e as regras do item são do processo e não
    /// mudam aqui; a cópia já foi conferida por <see cref="ConferirCopiaDoCatalogo"/>.
    /// </summary>
    internal void CopiarDoCatalogo(CopiaDoItemNoCatalogo copia)
    {
        CopiaDoItemNoCatalogo congelada = copia.NaFormaCongelada();
        OrigemValores = congelada.OrigemValores;
        Formato = congelada.Formato;
        ClassificacaoProtecao = congelada.ClassificacaoProtecao;
        HipoteseLegal = congelada.HipoteseLegal;
        FinalidadeTratamento = congelada.FinalidadeTratamento;
        Descricao = congelada.Descricao;
        ComponenteExibicao = congelada.ComponenteExibicao;
    }

    /// <summary>
    /// Adota do catálogo de hoje a descrição e o componente de exibição, que são apresentação: valem
    /// também no fato que alguma versão publicada já coletou, e a proteção dele não muda.
    /// </summary>
    internal void RecopiarApresentacaoDoCatalogo(CopiaDoItemNoCatalogo copia)
    {
        CopiaDoItemNoCatalogo congelada = copia.NaFormaCongelada();
        Descricao = congelada.Descricao;
        ComponenteExibicao = congelada.ComponenteExibicao;
    }

    /// <summary>O item no que o grafo do formulário confere.</summary>
    public ItemDoGrafo ParaGrafo() => new(FatoCodigo, Ordem, PaginaCodigo, FatosCitados);

    /// <summary>Indica se o fato é coletado incondicionalmente.</summary>
    public bool SemPrecondicao => _precondicoes.Count == 0;

    /// <summary>
    /// Códigos dos fatos citados pela pré-condição, pela obrigatoriedade, pelas restrições e pelo
    /// impedimento, sem repetição. O impedimento cita a resposta do próprio campo, que não é
    /// dependência: só as respostas anteriores entram.
    /// </summary>
    public IReadOnlyCollection<string> FatosCitados => Regras.FatosCitadosPor(FatoCodigo);

    /// <summary>Como o campo se apresenta ao candidato, pelo rótulo, tipo de campo, ajuda e pedido de confirmação que o item guarda.</summary>
    public ApresentacaoDoCampo Apresentacao => new(Rotulo, TipoDeCampo, Ajuda, PedirConfirmacao);

    /// <summary>As regras do campo, pela exibição, obrigatoriedade, restrições e impedimento que o item guarda.</summary>
    public RegrasDoCampo Regras => new(Exibicao, Obrigatoriedade, Restricoes, Impedimento);

    /// <summary>
    /// As condições das regras do item, para os vínculos e as referências a valor do processo: a
    /// pré-condição, a obrigatoriedade, o impedimento, as condições das opções e, porque as opções
    /// permitidas citam valores do próprio fato, a pertinência do fato a esses valores.
    /// </summary>
    public IEnumerable<CondicaoDnf> Condicoes => RegrasLidasDoCampo.De(Regras).CondicoesDoCampo(FatoCodigo);

    void IParteDoProcesso.VincularProcesso(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    internal void VincularGrupo(Guid grupoColetadoId) => GrupoColetadoId = grupoColetadoId;

    internal void VincularFormulario(FormularioProcesso formulario) => FormularioId = formulario.Id;

    /// <summary>
    /// A pré-condição de exibição na forma avaliável, ou <see langword="null"/> quando o fato é
    /// coletado incondicionalmente. Um predicado sem cláusula nenhuma avaliaria falso — que é o
    /// oposto de "sem pré-condição" —, então a ausência é representada pelo nulo, nunca por um
    /// predicado vazio.
    /// </summary>
    public PredicadoDnf? Exibicao =>
        _precondicoes.Count == 0
            ? null
            : PredicadoDnf.CriarDeCondicoesAgrupadas([.. _precondicoes.Select(static c => (c.Clausula, c.ParaCondicaoDnf()))]).Value!;
}

/// <summary>Códigos de erro de <see cref="FatoColetado"/>.</summary>
public static class FatoColetadoErrorCodes
{
    public const string ObrigatoriedadeInvalida = "FatoColetado.ObrigatoriedadeInvalida";
    public const string FatoDuplicado = "FatoColetado.FatoDuplicado";
    public const string PrecondicaoCitaFatoNaoColetado = "FatoColetado.PrecondicaoCitaFatoNaoColetado";
    public const string ClassificacaoProtecaoInvalida = "FatoColetado.ClassificacaoProtecaoInvalida";
    public const string ComponenteExibicaoInvalido = "FatoColetado.ComponenteExibicaoInvalido";
}
