namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Recusas das modalidades ofertadas: vaga ofertada, modalidade selecionada, ações quando indeferido e referência de reserva demográfica.
/// </summary>
internal static class ErrosDasModalidadesOfertadas
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        .. DaVagaOfertadaNoProcesso(),
        .. DosCamposObrigatoriosDaModalidade(),
        .. DaComposicaoDaModalidade(),
        .. DoRemanejamentoDaModalidade(),
        .. DaReferenciaDemograficaCongelada(),
    ];

    /// <summary>Vaga ofertada e modalidade repetida com regras ou ações diferentes entre as ofertas do processo.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaVagaOfertadaNoProcesso() =>
    [
        new("VagaOfertada.ModalidadeCodigoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.vaga_ofertada.modalidade_codigo_obrigatorio", "Código da modalidade da vaga ofertada é obrigatório")),
        new("ProcessoSeletivo.AcaoQuandoIndeferidoDivergente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.acao_quando_indeferido_divergente", "O mesmo código de modalidade não pode ter ações divergentes de vaga quando indeferido em ofertas distintas do processo")),
        new(ProcessoSeletivoErrorCodes.RegraDeAplicabilidadeDivergente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.regra_de_aplicabilidade_divergente", "A mesma modalidade foi copiada com regras de aplicabilidade diferentes em ofertas do processo")),
        new("ProcessoSeletivo.MunicipioDoBonusReferenciadoPorCondicaoViva", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.municipio_do_bonus_referenciado_por_condicao_viva", "Existe condição citando um município que deixaria a área do bônus regional")),
        new("ProcessoSeletivo.ModalidadeReferenciadaPorExigenciaViva", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.modalidade_referenciada_por_exigencia_viva", "Existe condição de gatilho documental referenciando um código de modalidade que deixaria de ser ofertado")),
    ];

    /// <summary>Identidade, código, natureza, composição e base legal da modalidade selecionada.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DosCamposObrigatoriosDaModalidade() =>
    [
        new("ModalidadeSelecionada.ModalidadeOrigemIdObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.modalidade_origem_id_obrigatorio", "Identidade da modalidade de origem é obrigatória")),
        new("ModalidadeSelecionada.CodigoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.codigo_obrigatorio", "Código da modalidade é obrigatório")),
        new("ModalidadeSelecionada.NaturezaLegalObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.natureza_legal_obrigatoria", "Natureza legal da modalidade é obrigatória")),
        new("ModalidadeSelecionada.ComposicaoVagasObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.composicao_vagas_obrigatoria", "Composição de vagas da modalidade é obrigatória")),
        new("ModalidadeSelecionada.BaseLegalObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.base_legal_obrigatoria", "Base legal da modalidade é obrigatória")),
    ];

    /// <summary>Origem da composição e cascata das cotas reservadas.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaComposicaoDaModalidade() =>
    [
        new("ModalidadeSelecionada.ComposicaoOrigemObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.composicao_origem_obrigatoria", "Modalidade com composição RETIRADA_DE_OUTRA_MODALIDADE exige o código de origem")),
        new("ModalidadeSelecionada.ComposicaoOrigemIndevida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.composicao_origem_indevida", "Código de origem só se aplica à composição RETIRADA_DE_OUTRA_MODALIDADE")),
        new("ModalidadeSelecionada.CotaReservadaExigeCascata", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.cota_reservada_exige_cascata", "Modalidade de cota reservada deve seguir a cascata legal")),
    ];

    /// <summary>Destino, par e fallback do remanejamento de uma modalidade selecionada.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DoRemanejamentoDaModalidade() =>
    [
        new("ModalidadeSelecionada.RemanejamentoDestinoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.remanejamento_destino_obrigatorio", "Modalidade com remanejamento DESTINO_UNICO exige o destino")),
        new("ModalidadeSelecionada.RemanejamentoCruzadoIncompleto", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.remanejamento_cruzado_incompleto", "Modalidade com remanejamento CRUZADO exige o par")),
        new("ModalidadeSelecionada.RemanejamentoDestinoIndevido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.remanejamento_destino_indevido", "Destino de remanejamento só se aplica à regra DESTINO_UNICO")),
        new("ModalidadeSelecionada.RemanejamentoCruzadoIndevido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.remanejamento_cruzado_indevido", "Par/fallback de remanejamento só se aplicam à regra CRUZADO")),
    ];

    /// <summary>Censo, percentuais e base legal do snapshot da referência demográfica.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaReferenciaDemograficaCongelada() =>
    [
        new("ReferenciaReservaDemograficaSnapshot.CensoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_reserva_demografica_snapshot.censo_obrigatorio", "Censo de referência é obrigatório")),
        new("ReferenciaReservaDemograficaSnapshot.PercentualInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_reserva_demografica_snapshot.percentual_invalido", "Os percentuais demográficos devem estar entre 0 e 100")),
        new("ReferenciaReservaDemograficaSnapshot.BaseLegalObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_reserva_demografica_snapshot.base_legal_obrigatoria", "Base legal é obrigatória")),
    ];
}
