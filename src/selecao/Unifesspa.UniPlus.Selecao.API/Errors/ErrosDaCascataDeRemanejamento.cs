namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Recusas da cascata de remanejamento de cotas (UNI-REQ-0056) e da coerência dela com as ofertas do processo.
/// </summary>
internal static class ErrosDaCascataDeRemanejamento
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        .. DaRegraDaCascata(),
        .. DaSequenciaDeDestinos(),
        .. DaCascataNoProcesso(),
        .. DaAplicabilidadeDasModalidades(),
    ];

    /// <summary>Campos, regra e matriz da cascata e o fallback dela.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaRegraDaCascata() =>
    [
        new("ConfiguracaoCascataRemanejamento.CamposObrigatorios", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_cascata_remanejamento.campos_obrigatorios", "RegraCodigo, RegraVersao, FallbackCodigo e Destinos precisam estar todos presentes ou todos ausentes")),
        new("ConfiguracaoCascataRemanejamento.RegraNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_cascata_remanejamento.regra_nao_encontrada", "Regra de remanejamento não encontrada no catálogo de regras")),
        new("ConfiguracaoCascataRemanejamento.RegraTipoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_cascata_remanejamento.regra_tipo_invalido", "A regra referenciada não é do tipo criterio_remanejamento")),
        new("ConfiguracaoCascataRemanejamento.MatrizDivergenteDaRegra", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_cascata_remanejamento.matriz_divergente_da_regra", "A cascata informada diverge do esquema_args da regra referenciada")),
        new("ConfiguracaoCascataRemanejamento.FallbackObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_cascata_remanejamento.fallback_obrigatorio", "O código de fallback é obrigatório e precisa casar com o vocabulário de modalidade")),
    ];

    /// <summary>Limites, ordem e repetição dos destinos de cada origem.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaSequenciaDeDestinos() =>
    [
        new("ConfiguracaoCascataRemanejamento.SemDestinos", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_cascata_remanejamento.sem_destinos", "A cascata precisa de ao menos um destino")),
        new("ConfiguracaoCascataRemanejamento.ExcedeLimiteDeOrigens", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_cascata_remanejamento.excede_limite_de_origens", "A cascata excede o limite de 8 origens")),
        new("ConfiguracaoCascataRemanejamento.ExcedeLimiteDeDestinos", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_cascata_remanejamento.excede_limite_de_destinos", "A cascata excede o limite de 56 destinos")),
        new("ConfiguracaoCascataRemanejamento.CodigoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_cascata_remanejamento.codigo_invalido", "Código de modalidade fora do vocabulário ^[A-Z0-9_]+$ ou acima de 60 caracteres")),
        new("ConfiguracaoCascataRemanejamento.OrdemDuplicadaNaOrigem", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_cascata_remanejamento.ordem_duplicada_na_origem", "A mesma origem tem mais de um destino na mesma posição de ordem")),
        new("ConfiguracaoCascataRemanejamento.OrdemInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_cascata_remanejamento.ordem_invalida", "A ordem do destino deve ser ≥ 1")),
        new("ConfiguracaoCascataRemanejamento.OrdemNaoContigua", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_cascata_remanejamento.ordem_nao_contigua", "A sequência de ordens de uma origem não é contígua a partir de 1")),
        new("ConfiguracaoCascataRemanejamento.DestinoDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_cascata_remanejamento.destino_duplicado", "A mesma origem repete o mesmo destino mais de uma vez")),
        new("ConfiguracaoCascataRemanejamento.OrigemIgualAoDestino", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_cascata_remanejamento.origem_igual_ao_destino", "Uma modalidade não pode remanejar para si mesma")),
    ];

    /// <summary>Origem, fallback e destino da cascata diante das ofertas do processo.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaCascataNoProcesso() =>
    [
        new("ProcessoSeletivo.CascataOrigemAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.cascata_origem_ausente", "Uma modalidade SegueCascata não tem sequência de destinos declarada na cascata do processo")),
        new("ProcessoSeletivo.CascataFallbackNaoSelecionadoNaOferta", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.cascata_fallback_nao_selecionado_na_oferta", "O fallback (ou o destino resolvível) da cascata não é modalidade selecionada na oferta")),
        new("ProcessoSeletivo.CascataDestinoDesconhecido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.cascata_destino_desconhecido", "A cascata declara destino que não é modalidade selecionada em nenhuma oferta do processo")),
        new("ProcessoSeletivo.CascataOrigemNaoSegueCascata", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.cascata_origem_nao_segue_cascata", "A cascata declara origem que nenhuma oferta do processo marca como SegueCascata")),
    ];

    /// <summary>Coerência das regras de aplicabilidade e de remanejamento com a natureza da modalidade e com a lei.</summary>
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaAplicabilidadeDasModalidades() =>
    [
        new("ProcessoSeletivo.CotaEAcaoAfirmativaDerivaveisJuntas", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.cota_e_acao_afirmativa_derivaveis_juntas", "As regras de aplicabilidade permitem que o mesmo candidato concorra a cota da lei e a ação afirmativa")),
        new(ProcessoSeletivoErrorCodes.RegraIncoerenteComANatureza, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.regra_incoerente_com_a_natureza", "A regra de aplicabilidade de uma modalidade ofertada não tem a forma que a natureza dela pede")),
        new(ProcessoSeletivoErrorCodes.RemanejamentoIncoerenteComANatureza, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.remanejamento_incoerente_com_a_natureza", "A regra de remanejamento de uma modalidade ofertada não é a que a natureza dela pede")),
        new(ProcessoSeletivoErrorCodes.RegraDeModalidadeCitaFatoNaoColetado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.regra_de_modalidade_cita_fato_nao_coletado", "A regra de aplicabilidade de uma modalidade ofertada cita um fato que o processo não coleta nem deriva")),
        new(ProcessoSeletivoErrorCodes.CotasDaLeiDiferentesEntreOfertas, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.cotas_da_lei_diferentes_entre_ofertas", "Os cursos do processo ofertam conjuntos diferentes de cotas da lei")),
        new(ProcessoSeletivoErrorCodes.ReservaPcdSemCotaPcdDaLei, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.reserva_pcd_sem_cota_pcd_da_lei", "O processo oferta a reserva de pessoa com deficiência de ampla concorrência e cota da lei, sem a cota da lei para pessoa com deficiência")),
        new("ProcessoSeletivo.CascataForaDoRegimeFederal", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.cascata_fora_do_regime_federal", "Uma modalidade SegueCascata está numa oferta que não usa regra de distribuição do ramo federal (Lei 12.711)")),
    ];
}
