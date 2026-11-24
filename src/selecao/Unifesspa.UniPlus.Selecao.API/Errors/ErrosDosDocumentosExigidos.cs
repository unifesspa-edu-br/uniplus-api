namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Recusas dos documentos exigidos e do vínculo deles com as fases.
/// </summary>
internal static class ErrosDosDocumentosExigidos
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        .. Exigencias(),
        .. ProtecaoDosDados(),
        .. FasesDasExigencias(),
        .. ReferenciasTemporais(),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Exigencias() =>
    [
        new("DocumentoExigido.AplicabilidadeObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.aplicabilidade_obrigatoria", "A aplicabilidade da exigência documental é obrigatória")),
        new("DocumentoExigido.ConsequenciaIndeferimentoInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.consequencia_indeferimento_invalida", "Consequência de indeferimento fora do domínio conhecido")),
        new("DocumentoExigido.GeralComCondicao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.geral_com_condicao", "A exigência geral não pode ter condição de aplicação ativa")),
        new("DocumentoExigido.FaseNaoPertenceAoProcesso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.fase_nao_pertence_ao_processo", "A fase informada não pertence ao cronograma deste processo")),
        new("DocumentoExigido.FinalidadeObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.finalidade_obrigatoria", "Documento exigido em fase de formulário sem o formulário a que pertence")),
        new("DocumentoExigido.FaseIncoerenteComFinalidade", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.fase_incoerente_com_finalidade", "A fase do documento não é a fase em que se responde o formulário a que ele pertence")),
        new("DocumentoExigido.FatosComprovadosInvalidos", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.fatos_comprovados_invalidos", "Os fatos comprovados pela exigência devem ser códigos de fato não repetidos")),
        new("DocumentoExigido.FatoComprovadoForaDoProcesso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.fato_comprovado_fora_do_processo", "A exigência comprova um fato que o processo não coleta e que não é derivado do catálogo")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> ProtecaoDosDados() =>
    [
        new(ProcessoSeletivoErrorCodes.ExigenciaMenosProtegidaQueFatoComprovado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.exigencia_menos_protegida_que_fato_comprovado", "O tipo de documento da exigência é menos protegido que um fato que ela comprova")),
        new("DocumentoExigido.CondicionalVaziaDeterminaResultado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.condicional_vazia_determina_resultado", "Exigência CONDICIONAL sem condição viva que determina resultado nunca seria cobrada de ninguém")),
        new(ProcessoSeletivoErrorCodes.TipoDocumentoNoVinculoComProtecaoPendente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.tipo_documento_com_protecao_pendente", "O tipo de documento aguarda a aprovação da proteção de dados e não aceita vínculo novo")),
        new(ProcessoSeletivoErrorCodes.TipoDocumentoComProtecaoPendente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.tipo_documento_com_protecao_pendente", "Tipo de documento do processo aguarda a aprovação da proteção de dados")),
        new(ProcessoSeletivoErrorCodes.FatoComProtecaoPendente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fato_com_protecao_pendente", "Fato do processo aguarda a aprovação da proteção de dados")),
        new(ProcessoSeletivoErrorCodes.TipoDocumentoDoVinculoNaoEncontrado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.tipo_documento_nao_encontrado", "Tipo de documento não encontrado ou não está mais vivo")),
        new("DocumentoExigido.ModeloNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.modelo_nao_encontrado", "O modelo de documento citado não existe neste processo")),
        new("DocumentoExigido.PontoResolucaoForaDoCronograma", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.ponto_resolucao_fora_do_cronograma", "A fase em que o fato citado na condição de aplicação é conhecido não pertence ao cronograma deste processo")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> FasesDasExigencias() =>
    [
        new("DocumentoExigido.FatoResolvidoEmFasePosterior", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.fato_resolvido_em_fase_posterior", "O documento é exigido por um fato que só se conhece numa fase posterior")),
        new("FaseCronograma.ReferenciadaPorExigenciaViva", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.referenciada_por_exigencia_viva", "A fase é usada pela referência do prazo ou da validade de uma exigência documental")),
        new("FaseCronograma.EtapaDaFaseReferenciada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.etapa_da_fase_referenciada", "A etapa que acontece na fase removida é referenciada pela classificação ou por um critério de desempate")),
        new("FaseCronograma.PendenciaReenvioExigeComplementacao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.pendencia_reenvio_exige_complementacao", "A fase não pode perder PermiteComplementacao — é referenciada por documento exigido com consequência ABRE_PRAZO_PARA_REENVIO")),
        new("FaseCronograma.PermutacaoDeOrdemNaoSuportada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.permutacao_de_ordem_nao_suportada", "A redefinição do cronograma troca a Ordem entre fases já existentes formando um ciclo fechado, que não pode ser persistido em uma única chamada")),
        new("CondicaoGatilho.ClausulaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.condicao_gatilho.clausula_invalida", "O ordinal da cláusula não pode ser negativo")),
        new("ReferenciaTemporalFatos.TipoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_temporal_fatos.tipo_obrigatorio", "O tipo de referência temporal é obrigatório")),
        new("ReferenciaTemporalFatos.TipoDesconhecido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_temporal_fatos.tipo_desconhecido", "Tipo de referência temporal fora do catálogo fechado")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> ReferenciasTemporais() =>
    [
        new("ReferenciaTemporalFatos.DataIncoerenteComTipo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_temporal_fatos.data_incoerente_com_tipo", "A data só é aceita (e é exigida) quando o tipo é DATA_ESPECIFICA")),
        new("ReferenciaTemporalFatos.FaseIncoerenteComTipo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_temporal_fatos.fase_incoerente_com_tipo", "A fase de referência só é aceita (e é exigida) quando o tipo é INICIO_FASE ou FIM_FASE")),
        new("ReferenciaTemporalFatos.FaseNaoPertenceAoProcesso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_temporal_fatos.fase_nao_pertence_ao_processo", "A fase de referência não pertence ao cronograma deste processo")),
        new("ProcessoSeletivo.ReferenciaTemporalFatosAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.referencia_temporal_fatos_ausente", "Uma regra cita FAIXA_ETARIA, mas nenhuma referência temporal de fatos foi configurada")),
        new("ProcessoSeletivo.ReferenciaTemporalFatosFaseInexistente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.referencia_temporal_fatos_fase_inexistente", "A fase da referência temporal de fatos não pertence (mais) ao cronograma")),
        new("ProcessoSeletivo.ReferenciaTemporalFatosExtremoAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.referencia_temporal_fatos_extremo_ausente", "A fase da referência temporal de fatos não tem o extremo (início/fim) definido")),
        new("ProcessoSeletivo.ReferenciaTemporalFatosFimInscricaoIndisponivel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.referencia_temporal_fatos_fim_inscricao_indisponivel", "FIM_INSCRICAO exige uma fase que colete inscrição com Fim definido")),
        new("ProcessoSeletivo.FatoColetadoSemValoresOfertados", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fato_coletado_sem_valores_ofertados", "O fato coletado é de seleção, mas o processo não declara nenhuma opção para ele")),
    ];
}
