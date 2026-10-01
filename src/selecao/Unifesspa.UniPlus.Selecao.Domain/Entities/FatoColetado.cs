namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text.Json;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Um fato do vocabulário que <b>este</b> processo seletivo coleta do candidato, com a sua
/// posição na ordem de coleta, a pré-condição que decide se o campo produtor é apresentado
/// (Story #926) e a apresentação do campo no formulário de inscrição — rótulo, tipo de
/// renderização e obrigatoriedade (Story #559).
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
public sealed class FatoColetado : EntityBase
{
    private readonly List<CondicaoPrecondicaoFato> _precondicoes = [];

    public Guid ProcessoSeletivoId { get; private set; }

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
    /// Como o campo é renderizado. A coerência contra o <c>Dominio</c> do fato no catálogo (ex.:
    /// domínio <c>BOOLEANO</c> só aceita <see cref="TipoRenderizacao.Booleano"/>) é validada na
    /// Application, que tem acesso ao vocabulário cross-módulo — o Domain não alcança o catálogo.
    /// </summary>
    public TipoRenderizacao TipoRenderizacao { get; private set; }

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
    /// fato no catálogo quando a coleta é definida — a Seleção congela por cópia e não lê o
    /// catálogo ao publicar.
    /// </summary>
    public OrigemValoresColeta OrigemValores { get; private set; }

    /// <summary>
    /// O formato da resposta de texto (livre, CPF, e-mail…), copiado do fato no catálogo quando a
    /// coleta é definida; existe se, e só se, o campo é de texto.
    /// </summary>
    public string? Formato { get; private set; }

    /// <summary>Se as opções do campo são as que o processo declara.</summary>
    public bool OpcoesDoProcesso => OrigemValores == OrigemValoresColeta.OpcoesDoProcesso;

    public IReadOnlyCollection<CondicaoPrecondicaoFato> Precondicoes => _precondicoes.AsReadOnly();

    /// <summary>A finalidade do formulário que produz o fato; atribuída pelo processo ao definir os itens.</summary>
    public FinalidadeFormulario Finalidade { get; private set; }

    /// <summary>A seção do formulário em que o item aparece.</summary>
    public string? EtapaCodigo { get; private set; }

    private FatoColetado() { }

    /// <summary>
    /// Acumula toda violação independente em vez de retornar na primeira (ADR-0125) — os
    /// quatro campos e a autorreferência de pré-condição não dependem uns dos outros. A
    /// mensagem de autorreferência não ecoa o código do fato (ADR-0023).
    /// </summary>
    public static Result<FatoColetado> Criar(
        string fatoCodigo,
        int ordem,
        string rotulo,
        TipoRenderizacao tipoRenderizacao,
        Obrigatoriedade obrigatoriedade,
        IReadOnlyList<CondicaoPrecondicaoFato>? precondicoes,
        OrigemValoresColeta origemValores = OrigemValoresColeta.Catalogo,
        string? etapaCodigo = null,
        FinalidadeFormulario finalidade = FinalidadeFormulario.Nenhuma,
        string? formato = null,
        string? ajuda = null,
        bool pedirConfirmacao = false,
        IReadOnlyList<RestricaoValor>? restricoes = null)
    {
        ArgumentNullException.ThrowIfNull(obrigatoriedade);
        IReadOnlyList<RestricaoValor> restricoesDoItem = restricoes ?? [];

        IReadOnlyList<CondicaoPrecondicaoFato> condicoes = precondicoes ?? [];
        List<FieldError> erros = FormaDoItem.Conferir(
            fatoCodigo, ordem, rotulo, tipoRenderizacao, formato, ajuda, condicoes.Select(static c => c.Fato), obrigatoriedade, restricoesDoItem);

        if (erros.Count > 0)
        {
            return Result<FatoColetado>.ValidationFailure(erros);
        }

        FatoColetado fato = new()
        {
            FatoCodigo = (fatoCodigo ?? string.Empty).Trim(),
            Ordem = ordem,
            Rotulo = (rotulo ?? string.Empty).Trim(),
            TipoRenderizacao = tipoRenderizacao,
            Obrigatoriedade = obrigatoriedade,
            Ajuda = FormaDoItem.TextoOpcional(ajuda),
            PedirConfirmacao = pedirConfirmacao,
            Restricoes = [.. restricoesDoItem.OrderBy(static r => r.Tipo)],
            OrigemValores = origemValores,
            Formato = FormaDoItem.TextoOpcional(formato),
            EtapaCodigo = string.IsNullOrWhiteSpace(etapaCodigo) ? null : etapaCodigo.Trim().Normalize(System.Text.NormalizationForm.FormC),

            // O processo atribui a finalidade ao definir os itens; quem remonta o envelope a informa.
            Finalidade = finalidade,
        };
        foreach (CondicaoPrecondicaoFato precondicao in condicoes)
        {
            precondicao.VincularFatoColetado(fato.Id);
            fato._precondicoes.Add(precondicao);
        }

        return Result<FatoColetado>.Success(fato);
    }

    /// <summary>Indica se o fato é coletado incondicionalmente.</summary>
    public bool SemPrecondicao => _precondicoes.Count == 0;

    /// <summary>Códigos dos fatos citados pela pré-condição, pela obrigatoriedade e pelas restrições, sem repetição.</summary>
    public IReadOnlyCollection<string> FatosCitados =>
        [.. _precondicoes.Select(static c => c.Fato)
            .Concat(Obrigatoriedade.FatosCitados)
            .Concat(Restricoes.SelectMany(static r => r.FatosCitados))
            .Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// As condições das regras do item, para os vínculos e as referências a valor do processo: a
    /// pré-condição, a obrigatoriedade, as condições das opções e, porque as opções permitidas citam
    /// valores do próprio fato, a pertinência do fato a esses valores.
    /// </summary>
    public IEnumerable<CondicaoDnf> Condicoes
    {
        get
        {
            OpcoesPermitidas? opcoes = Restricoes.OfType<OpcoesPermitidas>().SingleOrDefault();
            IEnumerable<CondicaoDnf> dasOpcoes = opcoes is null
                ? []
                : opcoes.Entradas.SelectMany(static e => e.Quando?.Clausulas ?? []).SelectMany(static c => c.Condicoes)
                    .Append(CondicaoDnf.Criar(
                        FatoCodigo, Operador.Em, JsonSerializer.SerializeToElement(opcoes.ValoresCitados.Order(StringComparer.Ordinal))).Value!);
            return _precondicoes.Select(static c => c.ParaCondicaoDnf())
                .Concat((Obrigatoriedade.Predicado?.Clausulas ?? []).SelectMany(static c => c.Condicoes))
                .Concat(dasOpcoes);
        }
    }

    internal void VincularProcessoSeletivo(Guid processoSeletivoId) =>
        ProcessoSeletivoId = processoSeletivoId;

    internal void VincularFinalidade(FinalidadeFormulario finalidade) => Finalidade = finalidade;

    /// <summary>
    /// O predicado de pré-condição na forma avaliável, ou <see langword="null"/> quando o fato é
    /// coletado incondicionalmente. Um predicado sem cláusula nenhuma avaliaria falso — que é o
    /// oposto de "sem pré-condição" —, então a ausência é representada pelo nulo, nunca por um
    /// predicado vazio.
    /// </summary>
    internal Regras.ValueObjects.PredicadoDnf? ParaPredicado() =>
        _precondicoes.Count == 0
            ? null
            : Regras.ValueObjects.PredicadoDnf.CriarDeCondicoesAgrupadas(
                [.. _precondicoes.Select(static c => (c.Clausula, c.ParaCondicaoDnf()))]).Value!;
}

/// <summary>Códigos de erro de <see cref="FatoColetado"/>.</summary>
public static class FatoColetadoErrorCodes
{
    public const string OpcoesDeOutroDominio = "FatoColetado.OpcoesDeOutroDominio";
    public const string OpcionalQueAlimentaRegra = "ProcessoSeletivo.CampoOpcionalQueAlimentaRegra";
    public const string ObrigatoriedadeInvalida = "FatoColetado.ObrigatoriedadeInvalida";
    public const string FatoDuplicado = "FatoColetado.FatoDuplicado";
    public const string OrdemDuplicada = "FatoColetado.OrdemDuplicada";
    public const string PrecondicaoCitaFatoNaoColetado = "FatoColetado.PrecondicaoCitaFatoNaoColetado";
    public const string PrecondicaoCitaFatoPosterior = "FatoColetado.PrecondicaoCitaFatoPosterior";
    public const string GrafoComCiclo = "FatoColetado.GrafoComCiclo";
}
