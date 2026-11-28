namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;

using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Modalidade de concorrência selecionada para a distribuição de vagas de uma
/// oferta (<see cref="ConfiguracaoDistribuicaoVagas"/>) — cópia por
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
    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }

    public string CodigoENome => $"{Codigo} — {Nome}";

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
    /// Cópia da ação de indeferimento do cadastro de <c>Modalidade</c>
    /// (Configuração) — código em letras maiúsculas, <c>RETIRA_A_MODALIDADE</c> quando declarada. <see langword="null"/> declara que a modalidade não
    /// reclassifica, e é assim que a conferência de coerência da publicação a lê: só confronta a
    /// consequência da exigência documental com esta ação quando ela está declarada. Sem
    /// ação, quem responde pelo destino do candidato indeferido é a própria exigência.
    /// </summary>
    public string? AcaoQuandoIndeferido { get; private set; }
    public string BaseLegal { get; private set; } = string.Empty;

    /// <summary>
    /// Quantidade de vagas fixada pelo edital para esta modalidade
    /// (ADR-0115) — só um campo de transporte. A obrigatoriedade/vedação
    /// (fixa no quadro fixado pelo edital e nas composições RETIRADA_DE_OUTRA_MODALIDADE/SUPLEMENTAR_AO_TOTAL;
    /// proibida nas calculadas) é validada em
    /// <see cref="ConfiguracaoDistribuicaoVagas.Criar"/>, que é quem conhece o
    /// tipo de distribuição — esta criação não tem essa informação.
    /// </summary>
    public int? QuantidadeDeclarada { get; private set; }

    /// <summary>
    /// O remanejamento é o que a natureza pede no cadastro: a ampla concorrência não remaneja as
    /// próprias vagas ociosas, e a ação afirmativa as devolve a um destino único ou as cruza com o
    /// par. A cota reservada, que segue a cascata legal, já é recusada na criação.
    /// </summary>
    /// <remarks>
    /// Fica fora de <see cref="Criar"/> porque esta é a cópia congelada de uma modalidade: a
    /// publicação recusa a cópia incoerente com erro nomeado, e a leitura da configuração publicada a
    /// recusa como malformada, as duas por esta mesma regra.
    /// </remarks>
    public static bool RemanejamentoCoerenteComANatureza(
        NaturezaLegalModalidade naturezaLegal, RegraRemanejamentoModalidade? regraRemanejamento) => naturezaLegal switch
        {
            NaturezaLegalModalidade.Ampla => regraRemanejamento == null,
            NaturezaLegalModalidade.AcaoAfirmativa => regraRemanejamento is RegraRemanejamentoModalidade.DestinoUnico or RegraRemanejamentoModalidade.Cruzado,
            _ => true,
        };

    private ModalidadeSelecionada() { }

    public static Result<ModalidadeSelecionada> Criar(ModalidadeNoCadastro modalidade, int? quantidadeDeclarada = null)
    {
        ArgumentNullException.ThrowIfNull(modalidade);
        ArgumentNullException.ThrowIfNull(modalidade.RegraDeAplicabilidade, nameof(modalidade));
        ArgumentException.ThrowIfNullOrWhiteSpace(modalidade.Nome, nameof(modalidade));
        IReadOnlyList<FieldError> recusas = Conferir(modalidade);
        return recusas.Count == 0
            ? Result<ModalidadeSelecionada>.Success(new ModalidadeSelecionada(modalidade, quantidadeDeclarada))
            : Result<ModalidadeSelecionada>.ValidationFailure(recusas);
    }

    private ModalidadeSelecionada(ModalidadeNoCadastro modalidade, int? quantidadeDeclarada)
    {
        ModalidadeOrigemId = modalidade.Id;
        Codigo = modalidade.Codigo;
        Nome = modalidade.Nome.Trim();
        Descricao = modalidade.Descricao;
        NaturezaLegal = modalidade.NaturezaLegal!.Value;
        ComposicaoVagas = modalidade.ComposicaoVagas!.Value;
        ComposicaoOrigemCodigo = modalidade.ComposicaoOrigemCodigo;
        CopiarRemanejamento(modalidade);
        RegraDeAplicabilidade = modalidade.RegraDeAplicabilidade;
        AcaoQuandoIndeferido = modalidade.AcaoQuandoIndeferido;
        BaseLegal = modalidade.BaseLegal.Trim();
        QuantidadeDeclarada = quantidadeDeclarada;
    }

    private void CopiarRemanejamento(ModalidadeNoCadastro modalidade)
    {
        RegraRemanejamento = modalidade.RegraRemanejamento;
        RemanejamentoDestino = modalidade.RemanejamentoDestino;
        RemanejamentoPar = modalidade.RemanejamentoPar;
        RemanejamentoFallback = modalidade.RemanejamentoFallback;
    }

    private static IReadOnlyList<FieldError> Conferir(ModalidadeNoCadastro modalidade) =>
        [.. RecusasDaIdentidade(modalidade), .. RecusasDoCadastroIncompleto(modalidade), .. RecusasDaComposicao(modalidade),
            .. RecusasDaCotaReservadaForaDaOrdemLegal(modalidade), .. RecusasDosParametrosDoRemanejamento(modalidade)];

    private static IEnumerable<FieldError> RecusaSe(bool recusada, DomainError recusa) =>
        recusada ? [new FieldError(null, recusa)] : [];

    private static IEnumerable<FieldError> RecusasDaIdentidade(ModalidadeNoCadastro modalidade) =>
    [
        .. RecusaSe(modalidade.Id == Guid.Empty, RecusasDaModalidadeSelecionada.OrigemObrigatoria()),
        .. RecusaSe(string.IsNullOrWhiteSpace(modalidade.Codigo), RecusasDaModalidadeSelecionada.CodigoObrigatorio()),
    ];

    private static IEnumerable<FieldError> RecusasDoCadastroIncompleto(ModalidadeNoCadastro modalidade) =>
    [
        .. RecusaSe(!NaturezaReconhecida(modalidade), RecusasDaModalidadeSelecionada.NaturezaLegalObrigatoria()),
        .. RecusaSe(!ComposicaoReconhecida(modalidade), RecusasDaModalidadeSelecionada.ComposicaoVagasObrigatoria()),
        .. RecusaSe(string.IsNullOrWhiteSpace(modalidade.BaseLegal), RecusasDaModalidadeSelecionada.BaseLegalObrigatoria()),
    ];

    private static bool NaturezaReconhecida(ModalidadeNoCadastro modalidade) =>
        modalidade.NaturezaLegal is not null && Enum.IsDefined(modalidade.NaturezaLegal.Value);

    private static bool ComposicaoReconhecida(ModalidadeNoCadastro modalidade) =>
        modalidade.ComposicaoVagas is not null && Enum.IsDefined(modalidade.ComposicaoVagas.Value);

    private static IEnumerable<FieldError> RecusasDaComposicao(ModalidadeNoCadastro modalidade) =>
        !ComposicaoReconhecida(modalidade) ? [] : modalidade.ComposicaoVagas == ComposicaoVagasModalidade.RetiradaDeOutraModalidade
        ? RecusaSe(string.IsNullOrWhiteSpace(modalidade.ComposicaoOrigemCodigo), RecusasDaModalidadeSelecionada.ComposicaoOrigemObrigatoria())
        : RecusaSe(modalidade.ComposicaoOrigemCodigo is not null, RecusasDaModalidadeSelecionada.ComposicaoOrigemIndevida());

    private static bool CotaReservadaForaDaOrdemLegal(ModalidadeNoCadastro modalidade) =>
        modalidade.NaturezaLegal == NaturezaLegalModalidade.CotaReservada
        && modalidade.RegraRemanejamento != RegraRemanejamentoModalidade.SegueOrdemDeRemanejamento;

    private static IEnumerable<FieldError> RecusasDaCotaReservadaForaDaOrdemLegal(ModalidadeNoCadastro modalidade) =>
        RecusaSe(CotaReservadaForaDaOrdemLegal(modalidade), RecusasDaModalidadeSelecionada.CotaReservadaExigeCascata());

    private static IEnumerable<FieldError> RecusasDosParametrosDoRemanejamento(ModalidadeNoCadastro modalidade) =>
        CotaReservadaForaDaOrdemLegal(modalidade) ? [] : modalidade.RegraRemanejamento switch
        {
            RegraRemanejamentoModalidade.DestinoUnico => RecusasDoDestinoUnico(modalidade),
            RegraRemanejamentoModalidade.Cruzado => RecusasDoCruzado(modalidade),
            _ => RecusasDeParametroNaoAdmitido(modalidade),
        };

    private static IEnumerable<FieldError> RecusasDoDestinoUnico(ModalidadeNoCadastro modalidade) =>
    [
        .. RecusaSe(string.IsNullOrWhiteSpace(modalidade.RemanejamentoDestino), RecusasDaModalidadeSelecionada.RemanejamentoDestinoObrigatorio()),
        .. RecusaSe(DeclaraParOuDestinoFinal(modalidade), RecusasDaModalidadeSelecionada.RemanejamentoCruzadoIndevido()),
    ];

    private static IEnumerable<FieldError> RecusasDoCruzado(ModalidadeNoCadastro modalidade) =>
    [
        .. RecusaSe(string.IsNullOrWhiteSpace(modalidade.RemanejamentoPar), RecusasDaModalidadeSelecionada.RemanejamentoCruzadoIncompleto()),
        .. RecusaSe(modalidade.RemanejamentoDestino is not null, RecusasDaModalidadeSelecionada.RemanejamentoDestinoIndevido()),
    ];

    private static IEnumerable<FieldError> RecusasDeParametroNaoAdmitido(ModalidadeNoCadastro modalidade) =>
    [
        .. RecusaSe(modalidade.RemanejamentoDestino is not null, RecusasDaModalidadeSelecionada.RemanejamentoDestinoIndevido()),
        .. RecusaSe(DeclaraParOuDestinoFinal(modalidade), RecusasDaModalidadeSelecionada.RemanejamentoCruzadoIndevido()),
    ];

    private static bool DeclaraParOuDestinoFinal(ModalidadeNoCadastro modalidade) =>
        modalidade.RemanejamentoPar is not null || modalidade.RemanejamentoFallback is not null;

    internal void VincularConfiguracao(Guid configuracaoDistribuicaoVagasId) =>
        ConfiguracaoDistribuicaoVagasId = configuracaoDistribuicaoVagasId;
}
