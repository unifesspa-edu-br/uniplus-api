namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Recusas das regras de comprovação e de suas bases legais.
/// </summary>
internal static class ErrosDasRegrasDeComprovacao
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        .. BaseLegalDoDocumento(),
        .. LimitesDoDocumento(),
        .. CompletudeDasReferencias(),
        .. Formatos(),
        .. ConsequenciasDoDocumento(),
        .. EstruturaDasRegras(),
        .. Grupos(),
        .. QuantidadeDeApresentacoes(),
        .. ConsequenciasERepeticao(),
        .. BaseLegalDoGrupo(),
        .. AvaliacaoDaComprovacao(),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> BaseLegalDoDocumento() =>
    [
        new("DocumentoExigidoBaseLegal.ReferenciaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido_base_legal.referencia_obrigatoria", "A referência da base legal é obrigatória")),
        new("DocumentoExigidoBaseLegal.AbrangenciaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido_base_legal.abrangencia_obrigatoria", "A abrangência da base legal é obrigatória")),
        new("DocumentoExigidoBaseLegal.SituacaoObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido_base_legal.situacao_obrigatoria", "A situação da base legal é obrigatória")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> LimitesDoDocumento() =>
    [
        new("DocumentoExigido.TamanhoMaximoBytesInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.tamanho_maximo_bytes_invalido", "O tamanho máximo em bytes, quando presente, deve ser maior que zero")),
        new("PrazoDeEmissao.DetalhesSemPrazo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.prazo_de_emissao.detalhes_sem_prazo", "Detalhes informados sem prazo de emissão")),
        new("PrazoDeEmissao.ValorInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.prazo_de_emissao.valor_invalido", "O valor do prazo de emissão deve ser maior que zero")),
        new("ValidadeDoDocumento.DetalhesSemExigencia", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.validade_do_documento.detalhes_sem_exigencia", "Detalhes informados sem exigência de validade")),
        new("DataDeReferenciaDoDocumento.Ausente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.data_de_referencia_do_documento.ausente", "A data de referência do documento é obrigatória")),
        new("DataDeReferenciaDoDocumento.DataIncoerenteComTipo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.data_de_referencia_do_documento.data_incoerente_com_tipo", "A data não corresponde ao tipo de referência")),
        new("DataDeReferenciaDoDocumento.FaseIncoerenteComTipo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.data_de_referencia_do_documento.fase_incoerente_com_tipo", "A fase não corresponde ao tipo de referência")),
        new("DataDeReferenciaDoDocumento.FaseNaoPertenceAoProcesso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.data_de_referencia_do_documento.fase_nao_pertence_ao_processo", "A fase da referência não pertence ao processo")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> CompletudeDasReferencias() =>
    [
        new("PrazoDeEmissao.EscolhaNaoDeclarada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.prazo_de_emissao.escolha_nao_declarada", "O prazo de emissão não foi escolhido")),
        new("ValidadeDoDocumento.EscolhaNaoDeclarada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.validade_do_documento.escolha_nao_declarada", "A validade do documento não foi escolhida")),
        new("DataDeReferenciaDoDocumento.DataDaFaseIndefinida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.data_de_referencia_do_documento.data_da_fase_indefinida", "A data da fase de referência não foi definida")),
        new("DataDeReferenciaDoDocumento.FimDasInscricoesIndefinido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.data_de_referencia_do_documento.fim_das_inscricoes_indefinido", "O fim das inscrições não foi definido")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Formatos() =>
    [
        new("FormatosPermitidos.Obrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formatos_permitidos.obrigatorio", "FormatosPermitidos é obrigatório: declare QUALQUER ou uma lista com ao menos um formato")),
        new("FormatosPermitidos.FormaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formatos_permitidos.forma_invalida", "FormatosPermitidos deve ser o token QUALQUER ou um array de formatos")),
        new("FormatosPermitidos.QualquerComFormatosEspecificos", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formatos_permitidos.qualquer_com_formatos_especificos", "QUALQUER não pode conviver com uma lista de formatos específicos")),
        new("FormatosPermitidos.FormatoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formatos_permitidos.formato_invalido", "Formato não reconhecido")),
        new("FormatosPermitidos.FormatoDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formatos_permitidos.formato_duplicado", "O mesmo formato aparece mais de uma vez na lista")),
        new("FormatosPermitidos.TamanhoMaximoBytesMaxInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formatos_permitidos.tamanho_maximo_bytes_max_invalido", "O tamanho máximo em bytes por formato, quando presente, deve ser maior que zero")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> ConsequenciasDoDocumento() =>
    [
        new("DocumentoExigido.RemoveVantagemSemVantagemViva", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.remove_vantagem_sem_vantagem_viva", "A exigência declara REMOVE_VANTAGEM, mas o processo não tem nenhuma vantagem viva para remover")),
        new("DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.consequencia_incoerente_com_acao_da_vaga", "A consequência de indeferimento da exigência é incoerente com a ação de indeferimento da modalidade que ela alcança")),
        new("DocumentoExigido.PendenciaReenvioExigeComplementacao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.pendencia_reenvio_exige_complementacao", "A fase não permite complementação — consequência ABRE_PRAZO_PARA_REENVIO exige PermiteComplementacao")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> EstruturaDasRegras() =>
    [
        new("RegraDeComprovacao.FormaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.forma_invalida", "Informe documento, todos os itens ou quantidade mínima de itens")),
        new("RegraDeComprovacao.DocumentoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.documento_obrigatorio", "A regra de documento precisa indicar a exigência documental")),
        new("RegraDeComprovacao.DocumentoProibidoEmGrupo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.documento_proibido_em_grupo", "O grupo de comprovação não pode indicar documento próprio")),
        new("RegraDeComprovacao.ItensProibidosEmRegraDeDocumento", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.itens_proibidos_em_regra_de_documento", "A regra de documento não pode conter outros itens")),
        new("RegraDeComprovacao.GrupoSemItens", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.grupo_sem_itens", "O grupo de comprovação precisa de pelo menos um item")),
        new("RegraDeComprovacao.RegraContemASiMesma", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.regra_contem_a_si_mesma", "Uma regra não pode conter a si mesma, diretamente ou dentro de outro grupo")),
        new("RegraDeComprovacao.GrupoComFasesDiferentes", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.grupo_com_fases_diferentes", "Os itens do grupo precisam pertencer à mesma fase")),
        new("RegraDeComprovacao.GrupoComFormulariosDiferentes", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.grupo_com_formularios_diferentes", "Os documentos do grupo precisam pertencer ao mesmo formulário")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Grupos() =>
    [
        new("RegraDeComprovacao.QuantidadeMinimaProibidaQuandoExigeTodos", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.quantidade_minima_proibida_quando_exige_todos", "O grupo que exige todos os itens não declara quantidade mínima")),
        new("RegraDeComprovacao.ConsequenciaProibidaQuandoExigeTodos", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.consequencia_proibida_quando_exige_todos", "O grupo que exige todos os itens não declara consequência própria")),
        new("RegraDeComprovacao.BaseLegalProibidaQuandoExigeTodos", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.base_legal_proibida_quando_exige_todos", "O grupo que exige todos os itens não declara base legal própria")),
        new("RegraDeComprovacao.QuantidadeMinimaDeItensForaDosLimites", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.quantidade_minima_de_itens_fora_dos_limites", "A quantidade mínima deve estar entre um e o total de itens do grupo")),
        new("RegraDeComprovacao.ConsequenciaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.consequencia_invalida", "A consequência informada não é reconhecida")),
        new("RegraDeComprovacao.BaseLegalSemConsequencia", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.base_legal_sem_consequencia", "A base legal própria do grupo exige consequência declarada")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> QuantidadeDeApresentacoes() =>
    [
        new("RegraDeComprovacao.QuantidadeMinimaDeApresentacoesInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.quantidade_minima_de_apresentacoes_invalida", "A quantidade mínima de apresentações deve estar entre 1 e 366")),
        new("RegraDeComprovacao.DataReferenciaIndevidaParaCriterio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.data_referencia_indevida_para_criterio", "O critério de distinção não aceita data de referência")),
        new("RegraDeComprovacao.OcorrenciasEsperadasIndevidasParaCriterio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.ocorrencias_esperadas_indevidas_para_criterio", "A lista só é aceita no critério por ocorrência")),
        new("RegraDeComprovacao.DataReferenciaObrigatoriaParaCalendario", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.data_referencia_obrigatoria_para_calendario", "Competência mensal e exercício anual exigem data de referência")),
        new("RegraDeComprovacao.OcorrenciasEsperadasVazias", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.ocorrencias_esperadas_vazias", "Informe pelo menos uma ocorrência esperada")),
        new("RegraDeComprovacao.OcorrenciaSemIdentificador", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.ocorrencia_sem_identificador", "Cada ocorrência esperada precisa de identificador")),
        new("RegraDeComprovacao.OcorrenciaComIdentificadorRepetido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.ocorrencia_com_identificador_repetido", "Os identificadores das ocorrências não podem se repetir")),
        new("RegraDeComprovacao.QuantidadeMinimaDiferenteDasOcorrencias", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.quantidade_minima_diferente_das_ocorrencias", "A quantidade mínima deve ser igual ao total de ocorrências esperadas")),
        new("RegraDeComprovacao.CriterioDeDistincaoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.criterio_de_distincao_invalido", "O critério de distinção das apresentações não é reconhecido")),
        new("RegraDeComprovacao.QuantidadeMinimaExcedeCalendario", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.quantidade_minima_excede_calendario", "A quantidade exige meses ou anos anteriores ao início do calendário representável")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> ConsequenciasERepeticao() =>
    [
        new("RegraDeComprovacao.RemoveVantagemSemVantagemConfigurada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.remove_vantagem_sem_vantagem_configurada", "O grupo determina retirar vantagem, mas nenhuma está configurada")),
        new("RegraDeComprovacao.ConsequenciaIncoerenteComAcaoDaVaga", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.consequencia_incoerente_com_acao_da_vaga", "A consequência não corresponde à ação de indeferimento da modalidade alcançada")),
        new("RegraDeComprovacao.GrupoParaRepeticaoNaoDeclarado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.grupo_para_repeticao_nao_declarado", "O grupo para repetição não existe no formulário do processo")),
        new("RegraDeComprovacao.RepeticaoDentroDeRepeticao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_de_comprovacao.repeticao_dentro_de_repeticao", "Uma regra repetida não pode conter outra regra de repetição")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> BaseLegalDoGrupo() =>
    [
        new("BaseLegalDaComprovacao.ReferenciaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.base_legal_da_comprovacao.referencia_obrigatoria", "A referência da base legal é obrigatória")),
        new("BaseLegalDaComprovacao.AbrangenciaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.base_legal_da_comprovacao.abrangencia_obrigatoria", "A abrangência da base legal é obrigatória")),
        new("BaseLegalDaComprovacao.SituacaoObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.base_legal_da_comprovacao.situacao_obrigatoria", "A situação da base legal é obrigatória")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> AvaliacaoDaComprovacao() =>
    [
        new("AvaliadorDaComprovacao.RegraSemDocumento", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.avaliacao_da_comprovacao.regra_sem_documento", "Uma regra de documento não indica a exigência documental")),
        new("AvaliadorDaComprovacao.DocumentoReferenciadoMaisDeUmaVez", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.avaliacao_da_comprovacao.documento_referenciado_mais_de_uma_vez", "A exigência documental aparece em mais de uma regra de comprovação")),
        new("AvaliadorDaComprovacao.OcorrenciaDoGrupoInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.avaliacao_da_comprovacao.ocorrencia_do_grupo_invalida", "Uma ocorrência do grupo tem identificador ausente ou repetido")),
    ];
}
