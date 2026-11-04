namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Recusas da base legal, do formato e da árvore de satisfação das exigências.
/// </summary>
internal static class ErrosDaArvoreDeExigencias
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        // Base legal 1:N (ADR-0074) — DocumentoExigidoBaseLegal
        // e o gate de publicação (ValidadorBaseLegalExigencias) aflora pelo
        // ProcessoSeletivo.ConformidadeInsuficiente já registrado acima, sem código novo.
        new("DocumentoExigidoBaseLegal.ReferenciaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido_base_legal.referencia_obrigatoria", "A referência da base legal é obrigatória")),
        new("DocumentoExigidoBaseLegal.AbrangenciaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido_base_legal.abrangencia_obrigatoria", "A abrangência da base legal é obrigatória")),
        new("DocumentoExigidoBaseLegal.StatusObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido_base_legal.status_obrigatorio", "O status da base legal é obrigatório")),
        // Idade máxima de emissão + formato + tamanho —
        // aviso, não bloqueio de presença (§1); coerência tudo-nulo OU completo do VO
        // IdadeMaximaEmissao e a checagem eager de fase âncora (mesma família estrutural
        // de ReferenciaTemporalFatos, mas por exigência, não por processo).
        new("DocumentoExigido.TamanhoMaximoBytesInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.tamanho_maximo_bytes_invalido", "O tamanho máximo em bytes, quando presente, deve ser maior que zero")),
        new("IdadeMaximaEmissao.CamposIncoerentesComAusencia", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.idade_maxima_emissao.campos_incoerentes_com_ausencia", "Data e a fase âncora só são aceitas quando a idade máxima de emissão está definida")),
        new("IdadeMaximaEmissao.CamposIncompletos", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.idade_maxima_emissao.campos_incompletos", "Valor, Unidade e ReferenciaTipo devem estar todos presentes, ou todos ausentes")),
        new("IdadeMaximaEmissao.ValorInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.idade_maxima_emissao.valor_invalido", "O valor da idade máxima de emissão deve ser maior que zero")),
        new("IdadeMaximaEmissao.DataIncoerenteComTipo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.idade_maxima_emissao.data_incoerente_com_tipo", "A data só é aceita (e é exigida) quando o tipo é DATA_ESPECIFICA")),
        new("IdadeMaximaEmissao.FaseIncoerenteComTipo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.idade_maxima_emissao.fase_incoerente_com_tipo", "A fase âncora só é aceita (e é exigida) quando o tipo é INICIO_FASE ou FIM_FASE")),
        new("IdadeMaximaEmissao.FaseNaoPertenceAoProcesso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.idade_maxima_emissao.fase_nao_pertence_ao_processo", "A fase âncora da idade máxima de emissão não pertence ao cronograma deste processo")),
        new("IdadeMaximaEmissao.FaseExtremoAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.idade_maxima_emissao.fase_extremo_ausente", "A fase âncora da idade máxima de emissão não tem o extremo (início/fim) definido")),
        // Formatos permitidos: substitui o campo singular FormatoPermitido? —
        // lista de {formato, tamanhoMaximoBytesMax} OU o token QUALQUER, mutuamente
        // exclusivos, campo agora obrigatório. Obrigatorio/FormaInvalida são produzidos pelo
        // handler ao interpretar o valor polimórfico do wire (JsonElement); os demais são do
        // próprio VO (FormatosPermitidos.Criar).
        new("FormatosPermitidos.Obrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formatos_permitidos.obrigatorio", "FormatosPermitidos é obrigatório: declare QUALQUER ou uma lista com ao menos um formato")),
        new("FormatosPermitidos.FormaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formatos_permitidos.forma_invalida", "FormatosPermitidos deve ser o token QUALQUER ou um array de formatos")),
        new("FormatosPermitidos.QualquerComFormatosEspecificos", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formatos_permitidos.qualquer_com_formatos_especificos", "QUALQUER não pode conviver com uma lista de formatos específicos")),
        new("FormatosPermitidos.FormatoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formatos_permitidos.formato_invalido", "Formato não reconhecido")),
        new("FormatosPermitidos.FormatoDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formatos_permitidos.formato_duplicado", "O mesmo formato aparece mais de uma vez na lista")),
        new("FormatosPermitidos.TamanhoMaximoBytesMaxInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formatos_permitidos.tamanho_maximo_bytes_max_invalido", "O tamanho máximo em bytes por formato, quando presente, deve ser maior que zero")),
        // Coerência consequência↔ação da vaga —
        // ProcessoSeletivo.PendenciaDeCoerenciaDaConsequenciaDeIndeferimento, chamado em
        // Publicar/Retificar/FecharRetificacao logo após as guardas de publicação.
        new("DocumentoExigido.RemoveVantagemSemVantagemViva", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.remove_vantagem_sem_vantagem_viva", "A exigência declara REMOVE_VANTAGEM, mas o processo não tem nenhuma vantagem viva para remover")),
        new("DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.consequencia_incoerente_com_acao_da_vaga", "A consequência de indeferimento da exigência é incoerente com a ação de indeferimento da modalidade que ela alcança")),
        // Gate PENDENCIA_REENVIO×PermiteComplementacao FORWARD — complementa
        // FaseCronograma.PendenciaReenvioExigeComplementacao (reverso, acima): aqui a escrita
        // da própria exigência/grupo é recusada quando a fase já não permite complementação.
        new("DocumentoExigido.PendenciaReenvioExigeComplementacao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.pendencia_reenvio_exige_complementacao", "A fase não permite complementação — consequência PENDENCIA_REENVIO exige PermiteComplementacao")),
        // Árvore de satisfação —
        // substitui o grupo de satisfação plano (GrupoSatisfacaoId, residual) por nós
        // folha/grupo E/OU. Invariantes de NoExigencia.CriarGrupo + erros de forma do handler
        // (tipo de nó desconhecido, folha sem documento).
        new("NoExigencia.OrdemInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.ordem_invalida", "A ordem do nó não pode ser negativa")),
        new("NoExigencia.GrupoVazio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.grupo_vazio", "Um grupo E/OU não pode ter zero filhos")),
        new("NoExigencia.ArvoreComCiclo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.arvore_com_ciclo", "A árvore de satisfação não pode conter ciclos")),
        new("NoExigencia.GrupoComFasesDiferentes", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.grupo_com_fases_diferentes", "Todos os nós de um grupo precisam pertencer à mesma fase do cronograma")),
        new("NoExigencia.GrupoComFinalidadesDiferentes", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.grupo_com_finalidades_diferentes", "Todos os documentos de um grupo pertencem ao mesmo formulário")),
        new("NoExigencia.QuantidadeMinimaProibidaEmGrupoE", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.quantidade_minima_proibida_em_grupo_e", "Um grupo E não tem cardinalidade própria — quantidadeMinima é proibida")),
        new("NoExigencia.ConsequenciaProibidaEmGrupoE", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.consequencia_proibida_em_grupo_e", "Um grupo E é transparente e não carrega consequência própria")),
        new("NoExigencia.BaseLegalProibidaEmGrupoE", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.base_legal_proibida_em_grupo_e", "Um grupo E é transparente e não carrega base legal própria")),
        new("NoExigencia.QuantidadeMinimaForaDosLimites", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.quantidade_minima_fora_dos_limites", "quantidadeMinima de um grupo OU/N-de deve estar entre 1 e o número de filhos")),
        new("NoExigencia.ConsequenciaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.consequencia_invalida", "Consequência do grupo fora do domínio conhecido")),
        new("NoExigencia.BaseLegalSemConsequencia", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.base_legal_sem_consequencia", "Base legal de grupo só é permitida quando o grupo carrega consequência")),
        new("NoExigencia.DocumentoObrigatorioEmFolha", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.documento_obrigatorio_em_folha", "Um nó do tipo FOLHA precisa declarar 'documento'")),
        new("NoExigencia.TipoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.tipo_invalido", "Tipo de nó inválido — esperado FOLHA, E ou OU")),
        // Cardinalidade qualificada de folha — NoExigencia.CriarFolha.
        new("NoExigencia.QuantidadeMinimaDeFolhaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.quantidade_minima_de_folha_invalida", "quantidadeMinima de uma folha, quando presente, deve estar entre 1 e 366")),
        new("NoExigencia.DataReferenciaIndevidaParaChave", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.data_referencia_indevida_para_chave", "dataReferencia não é aceita para a chaveDistincao informada")),
        new("NoExigencia.OcorrenciasEsperadasIndevidasParaChave", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.ocorrencias_esperadas_indevidas_para_chave", "ocorrenciasEsperadas só é aceita quando chaveDistincao é OCORRENCIA")),
        new("NoExigencia.DataReferenciaObrigatoriaParaChaveCalendario", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.data_referencia_obrigatoria_para_chave_calendario", "dataReferencia é obrigatória quando chaveDistincao é COMPETENCIA_MENSAL ou EXERCICIO_ANUAL")),
        new("NoExigencia.OcorrenciasEsperadasVazia", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.ocorrencias_esperadas_vazia", "ocorrenciasEsperadas, quando presente, não pode ser vazia")),
        new("NoExigencia.OcorrenciasEsperadasComIdVazio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.ocorrencias_esperadas_com_id_vazio", "Os identificadores de ocorrenciasEsperadas não podem ser vazios ou em branco")),
        new("NoExigencia.OcorrenciasEsperadasComIdsDuplicados", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.ocorrencias_esperadas_com_ids_duplicados", "Os identificadores de ocorrenciasEsperadas devem ser únicos")),
        new("NoExigencia.OcorrenciasEsperadasQuantidadeMinimaDivergente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.ocorrencias_esperadas_quantidade_minima_divergente", "quantidadeMinima deve ser igual ao número de ocorrenciasEsperadas")),
        new("NoExigencia.ChaveDistincaoInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.chave_distincao_invalida", "chaveDistincao fora do catálogo fechado")),
        new("NoExigencia.QuantidadeMinimaExcedeJanelaRepresentavel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.quantidade_minima_excede_janela_representavel", "quantidadeMinima excede o calendário representável a partir de dataReferencia")),
        // Coerência consequência↔ação da vaga, estendida ao grupo OU/N-de —
        // mesmos dois gates de DocumentoExigido acima, mesmo ponto de chamada.
        new("NoExigencia.RemoveVantagemSemVantagemViva", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.remove_vantagem_sem_vantagem_viva", "O grupo declara REMOVE_VANTAGEM, mas o processo não tem nenhuma vantagem viva para remover")),
        new("NoExigencia.ConsequenciaIncoerenteComAcaoDaVaga", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.consequencia_incoerente_com_acao_da_vaga", "A consequência do grupo é incoerente com a ação de indeferimento da modalidade que ele alcança")),
        // Repetição por entidade pelas ocorrências de um grupo repetível do formulário (ADR-0138).
        new("NoExigencia.TipoEntidadeInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.tipo_entidade_invalido", "repetePorEntidade não nomeia grupo repetível dos formulários do processo")),
        new("NoExigencia.RepeticaoDeEntidadeAninhada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.repeticao_de_entidade_aninhada", "Uma subárvore repetePorEntidade não pode conter outra — repetição não aninha")),
        // Base legal 1:N PRÓPRIA de grupo OU/N-de — mesmo shape/mensagens de
        // DocumentoExigidoBaseLegal acima.
        new("NoExigenciaBaseLegal.ReferenciaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia_base_legal.referencia_obrigatoria", "A referência da base legal é obrigatória")),
        new("NoExigenciaBaseLegal.AbrangenciaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia_base_legal.abrangencia_obrigatoria", "A abrangência da base legal é obrigatória")),
        new("NoExigenciaBaseLegal.StatusObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia_base_legal.status_obrigatorio", "O status da base legal é obrigatório")),
        // Resolvedor da árvore de satisfação (substitui ResolvedorExigencias.* do
        // grupo plano) — domain service puro, sem caller HTTP (o runtime de
        // coleta é fora de escopo); registrado por disciplina uniforme com o resto do módulo.
        new("ResolvedorArvoreSatisfacao.ArvoreAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.resolvedor_arvore_satisfacao.arvore_ausente", "Não há versão vigente congelada para resolver a árvore de exigências documentais")),
        new("ResolvedorArvoreSatisfacao.ArvoreEstruturalmenteInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.resolvedor_arvore_satisfacao.arvore_estruturalmente_invalida", "A árvore de exigências congelada tem identidade repetida entre folhas")),
        // Insumo de instâncias de entidade repetível (chamador, não a árvore congelada).
        new("ResolvedorArvoreSatisfacao.InstanciaEntidadeInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.resolvedor_arvore_satisfacao.instancia_entidade_invalida", "Uma instância de entidade declarada tem EntidadeId vazio ou duplicado")),
    ];
}
