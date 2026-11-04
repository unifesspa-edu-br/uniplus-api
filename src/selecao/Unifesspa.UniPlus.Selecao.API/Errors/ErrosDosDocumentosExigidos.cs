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
        // Documentos exigidos (Story #554, issue #547, PR #895) — núcleo: aplicabilidade
        // GERAL/CONDICIONAL, fase, snapshot-copy do tipo de documento e a guarda
        // fail-closed transitória (B-01) enquanto o bloco `documentosExigidos.exigencias`
        // do envelope segue stub (removida na PR #903, issue #548).
        new("DocumentoExigido.AplicabilidadeObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.aplicabilidade_obrigatoria", "A aplicabilidade da exigência documental é obrigatória")),
        new("DocumentoExigido.ConsequenciaIndeferimentoInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.consequencia_indeferimento_invalida", "Consequência de indeferimento fora do domínio conhecido")),
        new("DocumentoExigido.GeralComCondicao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.geral_com_condicao", "Exigência GERAL não pode conviver com condição de gatilho viva")),
        new("DocumentoExigido.FaseNaoPertenceAoProcesso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.fase_nao_pertence_ao_processo", "A fase informada não pertence ao cronograma deste processo")),
        // O formulário a que o documento pertence: obrigatório na fase que responde algum
        // formulário, e um dos que ela responde.
        new("DocumentoExigido.FinalidadeObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.finalidade_obrigatoria", "Documento exigido em fase de formulário sem o formulário a que pertence")),
        new("DocumentoExigido.FaseIncoerenteComFinalidade", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.fase_incoerente_com_finalidade", "A fase do documento não é a fase em que se responde o formulário a que ele pertence")),
        new("DocumentoExigido.FatosComprovadosInvalidos", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.fatos_comprovados_invalidos", "Os fatos comprovados pela exigência devem ser códigos de fato não repetidos")),
        new("DocumentoExigido.FatoComprovadoForaDoProcesso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.fato_comprovado_fora_do_processo", "A exigência comprova um fato que o processo não coleta e que não é derivado do catálogo")),
        new(ProcessoSeletivoErrorCodes.ExigenciaMenosProtegidaQueFatoComprovado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.exigencia_menos_protegida_que_fato_comprovado", "O tipo de documento da exigência é menos protegido que um fato que ela comprova")),
        new("DocumentoExigido.CondicionalVaziaDeterminaResultado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.condicional_vazia_determina_resultado", "Exigência CONDICIONAL sem condição viva que determina resultado nunca seria cobrada de ninguém")),
        new(ProcessoSeletivoErrorCodes.TipoDocumentoNoVinculoComProtecaoPendente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.tipo_documento_com_protecao_pendente", "O tipo de documento aguarda a aprovação da proteção de dados e não aceita vínculo novo")),
        new(ProcessoSeletivoErrorCodes.TipoDocumentoComProtecaoPendente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.tipo_documento_com_protecao_pendente", "Tipo de documento do processo aguarda a aprovação da proteção de dados")),
        new(ProcessoSeletivoErrorCodes.FatoComProtecaoPendente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fato_com_protecao_pendente", "Fato do processo aguarda a aprovação da proteção de dados")),
        new(ProcessoSeletivoErrorCodes.TipoDocumentoDoVinculoNaoEncontrado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.tipo_documento_nao_encontrado", "Tipo de documento não encontrado ou não está mais vivo")),
        new("DocumentoExigido.ModeloNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.modelo_nao_encontrado", "O modelo de documento citado não existe neste processo")),
        // Gate de fase (Story #916): uma condição de gatilho não pode citar um fato cujo
        // PontoResolucao é uma fase posterior à fase em que o documento é exigido — os dois
        // erros são diagnósticos distintos (fase do PontoResolucao ausente do cronograma vs.
        // presente, mas posterior), não reaproveitam o mesmo código.
        new("DocumentoExigido.PontoResolucaoForaDoCronograma", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.ponto_resolucao_fora_do_cronograma", "A fase em que o fato citado na condição de gatilho é conhecido não pertence ao cronograma deste processo")),
        new("DocumentoExigido.FatoResolvidoEmFasePosterior", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.fato_resolvido_em_fase_posterior", "O fato citado na condição de gatilho só é conhecido numa fase posterior à fase em que o documento é exigido")),
        // ProcessoSeletivo.ExigenciasDocumentaisNaoMaterializadas (guarda B-01) removido —
        // Story #554, PR #903, issue #548: o bloco deixou de ser stub, o gate real decide.
        new("FaseCronograma.ReferenciadaPorExigenciaViva", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.referenciada_por_exigencia_viva", "A fase removida do cronograma é usada por um documento de outra fase como âncora de idade máxima")),
        new("FaseCronograma.EtapaDaFaseReferenciada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.etapa_da_fase_referenciada", "A etapa que acontece na fase removida é referenciada pela classificação ou por um critério de desempate")),
        // Guards backward de fase (Story #554, PR #900, issue #893, CA-04) — complemento ao
        // guard acima: retirar PermiteComplementacao de uma fase referenciada por
        // exigência com consequência PENDENCIA_REENVIO.
        new("FaseCronograma.PendenciaReenvioExigeComplementacao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.pendencia_reenvio_exige_complementacao", "A fase não pode perder PermiteComplementacao — é referenciada por documento exigido com consequência PENDENCIA_REENVIO")),
        // Uma permutação cíclica de Ordem entre
        // fases retidas não tem ordem de UPDATE que a resolva num único SaveChanges.
        new("FaseCronograma.PermutacaoDeOrdemNaoSuportada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.permutacao_de_ordem_nao_suportada", "A redefinição do cronograma troca a Ordem entre fases já existentes formando um ciclo fechado, que não pode ser persistido em uma única chamada")),
        // Gatilho DNF (Story #554, PR #896, issue #892) — CondicaoGatilho sobre PredicadoDnf,
        // ReferenciaTemporalFatos e a validação de publicação sem fallback silencioso
        // (ADR-0111:235-236, B-03 do plano).
        new("CondicaoGatilho.ClausulaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.condicao_gatilho.clausula_invalida", "O ordinal da cláusula não pode ser negativo")),
        new("ReferenciaTemporalFatos.TipoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_temporal_fatos.tipo_obrigatorio", "O tipo de referência temporal é obrigatório")),
        new("ReferenciaTemporalFatos.TipoDesconhecido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_temporal_fatos.tipo_desconhecido", "Tipo de referência temporal fora do catálogo fechado")),
        new("ReferenciaTemporalFatos.DataIncoerenteComTipo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_temporal_fatos.data_incoerente_com_tipo", "A data só é aceita (e é exigida) quando o tipo é DATA_ESPECIFICA")),
        new("ReferenciaTemporalFatos.FaseIncoerenteComTipo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_temporal_fatos.fase_incoerente_com_tipo", "A fase âncora só é aceita (e é exigida) quando o tipo é INICIO_FASE ou FIM_FASE")),
        new("ReferenciaTemporalFatos.FaseNaoPertenceAoProcesso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_temporal_fatos.fase_nao_pertence_ao_processo", "A fase âncora não pertence ao cronograma deste processo")),
        new("ProcessoSeletivo.ReferenciaTemporalFatosAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.referencia_temporal_fatos_ausente", "Uma regra cita FAIXA_ETARIA, mas nenhuma referência temporal de fatos foi configurada")),
        new("ProcessoSeletivo.ReferenciaTemporalFatosFaseInexistente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.referencia_temporal_fatos_fase_inexistente", "A fase âncora da referência temporal de fatos não pertence (mais) ao cronograma")),
        new("ProcessoSeletivo.ReferenciaTemporalFatosExtremoAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.referencia_temporal_fatos_extremo_ausente", "A fase âncora da referência temporal de fatos não tem o extremo (início/fim) definido")),
        new("ProcessoSeletivo.ReferenciaTemporalFatosFimInscricaoIndisponivel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.referencia_temporal_fatos_fim_inscricao_indisponivel", "FIM_INSCRICAO exige uma fase que colete inscrição com Fim definido")),
        // issue #1077, generalizado pela #1619 — fato coletável cuja fonte dos valores é o processo,
        // sem nenhuma opção, publicaria um seletor sem opção para o candidato escolher.
        new("ProcessoSeletivo.FatoColetadoSemValoresOfertados", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fato_coletado_sem_valores_ofertados", "O fato coletado é de seleção, mas o processo não declara nenhuma opção para ele")),
    ];
}
