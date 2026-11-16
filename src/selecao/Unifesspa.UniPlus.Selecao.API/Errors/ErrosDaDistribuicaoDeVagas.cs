namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Recusas da distribuição de vagas: vagas do agregado-raiz, coerência de cada modalidade selecionada e o snapshot de referência demográfica.
/// </summary>
internal static class ErrosDaDistribuicaoDeVagas
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        .. DaComposicaoDasOfertas(),
        .. DaRegraDeDistribuicaoEDoAjuste(),
        .. DoTotalEDoPercentualDaOferta(),
        .. DasModalidadesSelecionadas(),
        .. DaDistribuicaoPelaLeiDeCotas(),
        .. DasReferenciasNaoEncontradas(),
    ];

    /// <summary>Distribuição ausente, oferta repetida e remanejamento cruzado ou para a ampla concorrência.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaComposicaoDasOfertas() =>
    [
        new("ProcessoSeletivo.DistribuicaoVagasVazia", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.distribuicao_vagas_vazia", "O processo deve ter ao menos uma distribuição de vagas configurada")),
        new("ProcessoSeletivo.OfertaCursoDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.oferta_curso_duplicada", "Cada oferta de curso só pode ter uma distribuição de vagas no processo")),
        new("ConfiguracaoDistribuicaoVagas.ParCruzadoNaoReciproco", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.par_cruzado_nao_reciproco", "Par de remanejamento cruzado não aponta de volta")),
        new("ConfiguracaoDistribuicaoVagas.ReclassificacaoParaAmplaSemAmplaOfertada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.reclassificacao_para_ampla_sem_ampla_ofertada", "Modalidade reclassifica na ampla concorrência, que não está ofertada")),
    ];

    /// <summary>Regra de distribuição, rol de modalidades que ela admite e ajuste do quadro.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaRegraDeDistribuicaoEDoAjuste() =>
    [
        new("ConfiguracaoDistribuicaoVagas.MotorDeAjusteVedadoNaLei12711", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.motor_de_ajuste_vedado_na_lei_de_cotas", "Motor de redução não se aplica à distribuição pela Lei 12.711")),
        new("ConfiguracaoDistribuicaoVagas.AjusteNaoAbsorveOExcesso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.ajuste_nao_absorve_o_excesso", "Modalidades do ajuste não comportam o excesso do quadro")),
        new("ConfiguracaoDistribuicaoVagas.AjusteReferenciaModalidadeForaDoQuadro", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.ajuste_referencia_modalidade_fora_do_quadro", "Ajuste reduz de modalidade fora das selecionadas")),
        new("ConfiguracaoDistribuicaoVagas.ModalidadeNaoAdmitidaPelaRegra", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.modalidade_nao_admitida_pela_regra", "Modalidade fora do rol que a regra de distribuição admite")),
        new("ConfiguracaoDistribuicaoVagas.ModalidadeDoRolAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.modalidade_do_rol_ausente", "Falta modalidade do rol fechado que a regra de distribuição exige")),
    ];

    /// <summary>Total de vagas ofertadas, quadro que não o completa e percentual da reserva.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DoTotalEDoPercentualDaOferta() =>
    [
        new("ConfiguracaoDistribuicaoVagas.VoBaseAcimaDasVagasAutorizadas", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.vo_base_acima_das_vagas_autorizadas", "VO_base acima das vagas anuais autorizadas da oferta")),
        new("ConfiguracaoDistribuicaoVagas.QuadroExcedeVoBase", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.quadro_excede_vo_base", "Quadro de vagas acima do total da oferta")),
        new("ConfiguracaoDistribuicaoVagas.QuadroNaoCompletaVoBase", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.quadro_nao_completa_vo_base", "Quadro de vagas abaixo do total da oferta")),
        new("ConfiguracaoDistribuicaoVagas.VoBaseInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.vo_base_invalido", "VO_base deve ser maior que zero")),
        new("ConfiguracaoDistribuicaoVagas.PrForaDoLimite", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.pr_fora_do_limite", "PR deve estar entre 0,5 e 1,0 (art. 10, II)")),
    ];

    /// <summary>Modalidades da oferta e a origem, o destino, o par e o fallback que cada uma cita.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DasModalidadesSelecionadas() =>
    [
        new("ConfiguracaoDistribuicaoVagas.ModalidadesVazias", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.modalidades_vazias", "A oferta deve ter ao menos uma modalidade selecionada")),
        new("ConfiguracaoDistribuicaoVagas.ModalidadeDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.modalidade_duplicada", "Cada modalidade só pode ser selecionada uma vez por oferta")),
        new("ConfiguracaoDistribuicaoVagas.ComposicaoOrigemNaoSelecionada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.composicao_origem_nao_selecionada", "A origem da composição RETIRADA_DE_OUTRA_MODALIDADE não está selecionada nesta oferta")),
        new("ConfiguracaoDistribuicaoVagas.RemanejamentoDestinoNaoSelecionado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.remanejamento_destino_nao_selecionado", "O destino do remanejamento DESTINO_UNICO não está selecionado nesta oferta")),
        new("ConfiguracaoDistribuicaoVagas.RemanejamentoParNaoSelecionado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.remanejamento_par_nao_selecionado", "O par do remanejamento CRUZADO não está selecionado nesta oferta")),
        new("ConfiguracaoDistribuicaoVagas.RemanejamentoFallbackNaoSelecionado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.remanejamento_fallback_nao_selecionado", "O fallback do remanejamento CRUZADO não está selecionado nesta oferta")),
    ];

    /// <summary>Exigências próprias da distribuição pela Lei 12.711.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaDistribuicaoPelaLeiDeCotas() =>
    [
        new("ConfiguracaoDistribuicaoVagas.ReferenciaDemograficaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.referencia_demografica_obrigatoria", "A distribuição pela Lei 12.711 exige a referência de reserva demográfica")),
        new("ConfiguracaoDistribuicaoVagas.ModalidadesFederaisIncompletas", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.modalidades_federais_incompletas", "A distribuição pela Lei 12.711 exige as 8 modalidades federais e AC")),
        new("ConfiguracaoDistribuicaoVagas.ReferenciaDemograficaIndevida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.referencia_demografica_indevida", "A referência de reserva demográfica só se aplica à distribuição pela Lei 12.711")),
    ];

    /// <summary>Oferta, regra, referência demográfica e modalidade que a configuração cita e não existem.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DasReferenciasNaoEncontradas() =>
    [
        new("ConfiguracaoDistribuicaoVagas.OfertaCursoNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.oferta_curso_nao_encontrada", "Oferta de curso não encontrada ou não está mais viva")),
        new("ConfiguracaoDistribuicaoVagas.RegraDistribuicaoNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.regra_distribuicao_nao_encontrada", "Regra de distribuição não encontrada no catálogo de regras")),
        new("ConfiguracaoDistribuicaoVagas.RegraDistribuicaoTipoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.regra_distribuicao_tipo_invalido", "A regra referenciada não é do tipo regra_distribuicao_vagas")),
        new("ConfiguracaoDistribuicaoVagas.ReferenciaDemograficaNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.referencia_demografica_nao_encontrada", "Referência de reserva demográfica não encontrada ou não está mais viva")),
        new("ConfiguracaoDistribuicaoVagas.ModalidadeNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.modalidade_nao_encontrada", "Modalidade não encontrada ou não está mais viva")),
    ];
}
