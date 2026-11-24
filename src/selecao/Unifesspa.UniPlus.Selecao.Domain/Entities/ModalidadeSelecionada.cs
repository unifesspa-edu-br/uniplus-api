namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Modalidade de concorrência selecionada para a distribuição de vagas de uma
/// oferta (<see cref="ConfiguracaoDistribuicaoVagas"/>) — snapshot-copy por
/// valor (ADR-0061) da <c>Modalidade</c> viva do módulo Configuração
/// (<c>IModalidadeReader</c>, ADR-0056) no momento da seleção. Entidade
/// interna do agregado <see cref="ProcessoSeletivo"/>: criada, substituída e
/// persistida exclusivamente pela raiz.
/// </summary>
/// <remarks>
/// Deriva de <see cref="EntityBase"/> puro (sem soft-delete), mesmo padrão de
/// <see cref="EtapaProcesso"/>: a configuração em rascunho é substituível por
/// inteiro (<see cref="ProcessoSeletivo.DefinirDistribuicaoVagas"/>).
/// </remarks>
public sealed class ModalidadeSelecionada : EntityBase
{
    public Guid ConfiguracaoDistribuicaoVagasId { get; private set; }
    public Guid ModalidadeOrigemId { get; private set; }
    public string Codigo { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
    public NaturezaLegalModalidade NaturezaLegal { get; private set; }
    public ComposicaoVagasModalidade ComposicaoVagas { get; private set; }
    public string? ComposicaoOrigemCodigo { get; private set; }
    public RegraRemanejamentoModalidade? RegraRemanejamento { get; private set; }

    public string? RemanejamentoDestino { get; private set; }
    public string? RemanejamentoPar { get; private set; }
    public string? RemanejamentoFallback { get; private set; }

    /// <summary>
    /// Quem concorre à modalidade, pelos fatos do candidato (ADR-0144): a regra de aplicabilidade
    /// do cadastro, congelada quando a distribuição é definida. Sem cláusula, a modalidade é
    /// incondicional.
    /// </summary>
    public PredicadoDnf RegraDeAplicabilidade { get; private set; } = null!;

    /// <summary>
    /// Snapshot-copy da ação de indeferimento do cadastro de <c>Modalidade</c>
    /// (Configuração) — token UPPER_SNAKE, <c>RECLASSIFICAR_AC</c> ou
    /// <c>RECLASSIFICAR_REGRA_EDITAL</c>. <see langword="null"/> declara que a modalidade não
    /// reclassifica, e é assim que o gate de coerência da publicação a lê: só confronta a
    /// consequência da exigência documental com esta ação quando ela está declarada. Sem
    /// ação, quem responde pelo destino do candidato indeferido é a própria exigência.
    /// </summary>
    public string? AcaoQuandoIndeferido { get; private set; }
    public string BaseLegal { get; private set; } = string.Empty;

    /// <summary>
    /// Quantidade de vagas fixada pelo edital para esta modalidade (issue
    /// #848/ADR-0115) — só um campo de transporte. A obrigatoriedade/vedação
    /// (fixa no ramo institucional e nas composições RETIRADA_DE_OUTRA_MODALIDADE/SUPLEMENTAR_AO_TOTAL;
    /// proibida nas calculadas) é validada em
    /// <see cref="ConfiguracaoDistribuicaoVagas.Criar"/>, que é quem conhece o
    /// ramo de distribuição — esta factory não tem essa informação.
    /// </summary>
    public int? QuantidadeDeclarada { get; private set; }

    /// <summary>
    /// O remanejamento é o que a natureza pede no cadastro: a ampla concorrência não remaneja as
    /// próprias vagas ociosas, e a ação afirmativa as devolve a um destino único ou as cruza com o
    /// par. A cota reservada, que segue a cascata legal, já é recusada na criação.
    /// </summary>
    /// <remarks>
    /// Fica fora de <see cref="Criar"/> porque esta é a cópia congelada de uma modalidade: a
    /// publicação recusa a cópia incoerente com erro nomeado, e a leitura do envelope publicado a
    /// recusa como malformada, as duas por esta mesma regra.
    /// </remarks>
    public bool RemanejamentoCoerenteComANatureza() => NaturezaLegal switch
    {
        NaturezaLegalModalidade.Ampla => RegraRemanejamento == null,
        NaturezaLegalModalidade.AcaoAfirmativa => RegraRemanejamento is RegraRemanejamentoModalidade.DestinoUnico or RegraRemanejamentoModalidade.Cruzado,
        _ => true,
    };

    private ModalidadeSelecionada() { }

    public static Result<ModalidadeSelecionada> Criar(ModalidadeNoCadastro modalidade, int? quantidadeDeclarada = null)
    {
        ArgumentNullException.ThrowIfNull(modalidade);
        ArgumentNullException.ThrowIfNull(modalidade.RegraDeAplicabilidade, nameof(modalidade));
        DomainError? recusa = RecusaDaIdentidade(modalidade) ?? RecusaDoCadastroIncompleto(modalidade)
            ?? RecusaDaComposicao(modalidade) ?? RecusaDaCotaReservadaForaDaOrdemLegal(modalidade)
            ?? RecusaDosParametrosDoRemanejamento(modalidade);
        return recusa is null ? Result<ModalidadeSelecionada>.Success(new ModalidadeSelecionada(modalidade, quantidadeDeclarada))
            : Result<ModalidadeSelecionada>.Failure(recusa);
    }

    private ModalidadeSelecionada(ModalidadeNoCadastro modalidade, int? quantidadeDeclarada)
    {
        ModalidadeOrigemId = modalidade.Id;
        Codigo = modalidade.Codigo.Trim();
        Descricao = modalidade.Descricao;
        NaturezaLegal = modalidade.NaturezaLegal!.Value;
        ComposicaoVagas = modalidade.ComposicaoVagas!.Value;
        ComposicaoOrigemCodigo = modalidade.ComposicaoOrigemCodigo;
        RegraRemanejamento = modalidade.RegraRemanejamento;
        RemanejamentoDestino = modalidade.RemanejamentoDestino;
        RemanejamentoPar = modalidade.RemanejamentoPar;
        RemanejamentoFallback = modalidade.RemanejamentoFallback;
        RegraDeAplicabilidade = modalidade.RegraDeAplicabilidade;
        AcaoQuandoIndeferido = modalidade.AcaoQuandoIndeferido;
        BaseLegal = modalidade.BaseLegal.Trim();
        QuantidadeDeclarada = quantidadeDeclarada;
    }

    // A identidade permite reconhecer a modalidade exigida legalmente (ADR-0129).
    private static DomainError? RecusaDaIdentidade(ModalidadeNoCadastro m) =>
        m.Id == Guid.Empty ? RecusasDaModalidadeSelecionada.OrigemObrigatoria()
        : string.IsNullOrWhiteSpace(m.Codigo) ? RecusasDaModalidadeSelecionada.CodigoObrigatorio() : null;

    private static DomainError? RecusaDoCadastroIncompleto(ModalidadeNoCadastro m) =>
        m.NaturezaLegal is null || !Enum.IsDefined(m.NaturezaLegal.Value) ? RecusasDaModalidadeSelecionada.NaturezaLegalObrigatoria(m.Codigo)
        : m.ComposicaoVagas is null || !Enum.IsDefined(m.ComposicaoVagas.Value) ? RecusasDaModalidadeSelecionada.ComposicaoVagasObrigatoria(m.Codigo)
        : string.IsNullOrWhiteSpace(m.BaseLegal) ? RecusasDaModalidadeSelecionada.BaseLegalObrigatoria(m.Codigo) : null;

    private static DomainError? RecusaDaComposicao(ModalidadeNoCadastro m) =>
        m.ComposicaoVagas == ComposicaoVagasModalidade.RetiradaDeOutraModalidade
            ? string.IsNullOrWhiteSpace(m.ComposicaoOrigemCodigo) ? RecusasDaModalidadeSelecionada.ComposicaoOrigemObrigatoria(m.Codigo) : null
            : m.ComposicaoOrigemCodigo is not null ? RecusasDaModalidadeSelecionada.ComposicaoOrigemIndevida(m.Codigo) : null;

    private static DomainError? RecusaDaCotaReservadaForaDaOrdemLegal(ModalidadeNoCadastro m) =>
        m.NaturezaLegal == NaturezaLegalModalidade.CotaReservada && m.RegraRemanejamento != RegraRemanejamentoModalidade.SegueOrdemDeRemanejamento
            ? RecusasDaModalidadeSelecionada.CotaReservadaExigeCascata(m.Codigo) : null;

    private static DomainError? RecusaDosParametrosDoRemanejamento(ModalidadeNoCadastro m) => m.RegraRemanejamento switch
    {
        RegraRemanejamentoModalidade.DestinoUnico => RecusaDoDestinoUnico(m),
        RegraRemanejamentoModalidade.Cruzado => RecusaDoCruzado(m),
        _ => RecusaDeParametroNaoAdmitido(m),
    };

    private static DomainError? RecusaDoDestinoUnico(ModalidadeNoCadastro m) =>
        string.IsNullOrWhiteSpace(m.RemanejamentoDestino) ? RecusasDaModalidadeSelecionada.RemanejamentoDestinoObrigatorio(m.Codigo)
        : m.RemanejamentoPar is not null || m.RemanejamentoFallback is not null ? RecusasDaModalidadeSelecionada.RemanejamentoCruzadoIndevido(m.Codigo) : null;

    // O destino final é opcional no cruzamento; o par é obrigatório (UNI-REQ-0096).
    private static DomainError? RecusaDoCruzado(ModalidadeNoCadastro m) =>
        string.IsNullOrWhiteSpace(m.RemanejamentoPar) ? RecusasDaModalidadeSelecionada.RemanejamentoCruzadoIncompleto(m.Codigo)
        : m.RemanejamentoDestino is not null ? RecusasDaModalidadeSelecionada.RemanejamentoDestinoIndevido(m.Codigo) : null;

    private static DomainError? RecusaDeParametroNaoAdmitido(ModalidadeNoCadastro m) =>
        m.RemanejamentoDestino is not null ? RecusasDaModalidadeSelecionada.RemanejamentoDestinoIndevido(m.Codigo)
        : m.RemanejamentoPar is not null || m.RemanejamentoFallback is not null ? RecusasDaModalidadeSelecionada.RemanejamentoCruzadoIndevido(m.Codigo) : null;

    internal void VincularConfiguracao(Guid configuracaoDistribuicaoVagasId) =>
        ConfiguracaoDistribuicaoVagasId = configuracaoDistribuicaoVagasId;
}
