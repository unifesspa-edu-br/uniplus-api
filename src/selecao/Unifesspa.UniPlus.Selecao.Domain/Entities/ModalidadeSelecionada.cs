namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Enums;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

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
    public RegraRemanejamentoModalidade RegraRemanejamento { get; private set; }
    public string? RemanejamentoDestino { get; private set; }
    public string? RemanejamentoPar { get; private set; }
    public string? RemanejamentoFallback { get; private set; }
    public IReadOnlyList<string> CriteriosCumulativos { get; private set; } = [];
    public string? AcaoQuandoIndeferido { get; private set; }
    public string BaseLegal { get; private set; } = string.Empty;

    /// <summary>
    /// Quantidade de vagas fixada pelo edital para esta modalidade (issue
    /// #848/ADR-0115) — só um campo de transporte. A obrigatoriedade/vedação
    /// (fixa no ramo institucional e nas composições RETIRA_DE/SUPLEMENTAR_AO_TOTAL;
    /// proibida nas calculadas) é validada em
    /// <see cref="ConfiguracaoDistribuicaoVagas.Criar"/>, que é quem conhece o
    /// ramo de distribuição — esta factory não tem essa informação.
    /// </summary>
    public int? QuantidadeDeclarada { get; private set; }

    private ModalidadeSelecionada() { }

    /// <summary>
    /// Cria a modalidade selecionada a partir do snapshot lido de
    /// <c>ModalidadeView</c>, validando as invariantes que dependem só dos
    /// próprios campos (INV-2, INV-12 e a coerência interna de composição e
    /// remanejamento).
    /// </summary>
    public static Result<ModalidadeSelecionada> Criar(
        Guid modalidadeOrigemId,
        string codigo,
        string? descricao,
        NaturezaLegalModalidade naturezaLegal,
        ComposicaoVagasModalidade composicaoVagas,
        string? composicaoOrigemCodigo,
        RegraRemanejamentoModalidade regraRemanejamento,
        string? remanejamentoDestino,
        string? remanejamentoPar,
        string? remanejamentoFallback,
        IReadOnlyList<string> criteriosCumulativos,
        string? acaoQuandoIndeferido,
        string baseLegal,
        int? quantidadeDeclarada = null)
    {
        ArgumentNullException.ThrowIfNull(criteriosCumulativos);

        if (string.IsNullOrWhiteSpace(codigo))
        {
            return Result<ModalidadeSelecionada>.Failure(new DomainError(
                "ModalidadeSelecionada.CodigoObrigatorio", "Código da modalidade é obrigatório."));
        }

        if (quantidadeDeclarada is < 0)
        {
            return Result<ModalidadeSelecionada>.Failure(new DomainError(
                "ConfiguracaoDistribuicaoVagas.QuantidadeVagaNegativa",
                $"A quantidade de vagas declarada para {codigo} não pode ser negativa ({quantidadeDeclarada})."));
        }

        // INV-2: natureza_legal, composicao_vagas e base_legal completos.
        if (naturezaLegal == NaturezaLegalModalidade.Nenhuma)
        {
            return Result<ModalidadeSelecionada>.Failure(new DomainError(
                "ModalidadeSelecionada.NaturezaLegalObrigatoria",
                $"Natureza legal da modalidade {codigo} é obrigatória (INV-2)."));
        }

        if (composicaoVagas == ComposicaoVagasModalidade.Nenhuma)
        {
            return Result<ModalidadeSelecionada>.Failure(new DomainError(
                "ModalidadeSelecionada.ComposicaoVagasObrigatoria",
                $"Composição de vagas da modalidade {codigo} é obrigatória (INV-2)."));
        }

        if (string.IsNullOrWhiteSpace(baseLegal))
        {
            return Result<ModalidadeSelecionada>.Failure(new DomainError(
                "ModalidadeSelecionada.BaseLegalObrigatoria",
                $"Base legal da modalidade {codigo} é obrigatória (INV-2)."));
        }

        // Coerência RETIRA_DE: exige origem; as demais composições não a têm.
        bool retiraDe = composicaoVagas == ComposicaoVagasModalidade.RetiraDe;
        if (retiraDe && string.IsNullOrWhiteSpace(composicaoOrigemCodigo))
        {
            return Result<ModalidadeSelecionada>.Failure(new DomainError(
                "ModalidadeSelecionada.ComposicaoOrigemObrigatoria",
                $"Modalidade {codigo} com composição RETIRA_DE exige o código de origem."));
        }

        if (!retiraDe && composicaoOrigemCodigo is not null)
        {
            return Result<ModalidadeSelecionada>.Failure(new DomainError(
                "ModalidadeSelecionada.ComposicaoOrigemIndevida",
                $"Modalidade {codigo} só declara código de origem quando a composição é RETIRA_DE."));
        }

        // INV-12: cota reservada da Lei 12.711 obriga a cascata legal — o admin
        // não pode trocar por destino único ou remanejamento cruzado.
        if (naturezaLegal == NaturezaLegalModalidade.CotaReservada
            && regraRemanejamento != RegraRemanejamentoModalidade.SegueCascata)
        {
            return Result<ModalidadeSelecionada>.Failure(new DomainError(
                "ModalidadeSelecionada.CotaReservadaExigeCascata",
                $"Modalidade {codigo} é cota reservada da Lei 12.711 e deve seguir a cascata legal (INV-12)."));
        }

        // Coerência do remanejamento: cada regra exige seus próprios campos e
        // não aceita os das demais.
        bool destinoUnico = regraRemanejamento == RegraRemanejamentoModalidade.DestinoUnico;
        bool cruzado = regraRemanejamento == RegraRemanejamentoModalidade.Cruzado;

        if (destinoUnico && string.IsNullOrWhiteSpace(remanejamentoDestino))
        {
            return Result<ModalidadeSelecionada>.Failure(new DomainError(
                "ModalidadeSelecionada.RemanejamentoDestinoObrigatorio",
                $"Modalidade {codigo} com remanejamento DESTINO_UNICO exige o destino."));
        }

        if (cruzado && (string.IsNullOrWhiteSpace(remanejamentoPar) || string.IsNullOrWhiteSpace(remanejamentoFallback)))
        {
            return Result<ModalidadeSelecionada>.Failure(new DomainError(
                "ModalidadeSelecionada.RemanejamentoCruzadoIncompleto",
                $"Modalidade {codigo} com remanejamento CRUZADO exige par e fallback."));
        }

        if (!destinoUnico && remanejamentoDestino is not null)
        {
            return Result<ModalidadeSelecionada>.Failure(new DomainError(
                "ModalidadeSelecionada.RemanejamentoDestinoIndevido",
                $"Modalidade {codigo} só declara destino de remanejamento quando a regra é DESTINO_UNICO."));
        }

        if (!cruzado && (remanejamentoPar is not null || remanejamentoFallback is not null))
        {
            return Result<ModalidadeSelecionada>.Failure(new DomainError(
                "ModalidadeSelecionada.RemanejamentoCruzadoIndevido",
                $"Modalidade {codigo} só declara par/fallback de remanejamento quando a regra é CRUZADO."));
        }

        return Result<ModalidadeSelecionada>.Success(new ModalidadeSelecionada
        {
            ModalidadeOrigemId = modalidadeOrigemId,
            Codigo = codigo.Trim(),
            Descricao = descricao,
            NaturezaLegal = naturezaLegal,
            ComposicaoVagas = composicaoVagas,
            ComposicaoOrigemCodigo = composicaoOrigemCodigo,
            RegraRemanejamento = regraRemanejamento,
            RemanejamentoDestino = remanejamentoDestino,
            RemanejamentoPar = remanejamentoPar,
            RemanejamentoFallback = remanejamentoFallback,
            CriteriosCumulativos = [.. criteriosCumulativos],
            AcaoQuandoIndeferido = acaoQuandoIndeferido,
            BaseLegal = baseLegal.Trim(),
            QuantidadeDeclarada = quantidadeDeclarada,
        });
    }

    internal void VincularConfiguracao(Guid configuracaoDistribuicaoVagasId) =>
        ConfiguracaoDistribuicaoVagasId = configuracaoDistribuicaoVagasId;
}
