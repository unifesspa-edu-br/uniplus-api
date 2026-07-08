namespace Unifesspa.UniPlus.Selecao.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, SelecaoDomainErrorRegistration>().")]
internal sealed class SelecaoDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        // ObrigatoriedadeLegal forma plena (Story #460, ADR-0058). Códigos do
        // placeholder #459 preservados; novos códigos refletem invariantes da
        // forma plena (vigência, governance, hash UNIQUE, regra duplicada).
        new("ObrigatoriedadeLegal.RegraCodigoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.regra_codigo_obrigatorio", "RegraCodigo obrigatório")),
        new("ObrigatoriedadeLegal.RegraCodigoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.regra_codigo_invalido", "RegraCodigo inválido")),
        new("ObrigatoriedadeLegal.RegraCodigoDuplicada", new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.selecao.obrigatoriedade_legal.regra_codigo_duplicada", "Já existe regra ativa com este RegraCodigo")),
        new("ObrigatoriedadeLegal.PredicadoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.predicado_obrigatorio", "Predicado obrigatório")),
        new("ObrigatoriedadeLegal.BaseLegalObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.base_legal_obrigatoria", "BaseLegal obrigatória")),
        new("ObrigatoriedadeLegal.BaseLegalInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.base_legal_invalida", "BaseLegal inválida")),
        new("ObrigatoriedadeLegal.DescricaoHumanaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.descricao_humana_obrigatoria", "DescricaoHumana obrigatória")),
        new("ObrigatoriedadeLegal.DescricaoHumanaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.descricao_humana_invalida", "DescricaoHumana inválida")),
        new("ObrigatoriedadeLegal.TipoEditalCodigoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.tipo_edital_codigo_obrigatorio", "TipoEditalCodigo obrigatório")),
        new("ObrigatoriedadeLegal.TipoEditalCodigoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.tipo_edital_codigo_invalido", "TipoEditalCodigo inválido")),
        new("ObrigatoriedadeLegal.CategoriaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.categoria_invalida", "Categoria inválida")),
        new("ObrigatoriedadeLegal.AtoNormativoUrlInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.ato_normativo_url_invalido", "AtoNormativoUrl inválido")),
        new("ObrigatoriedadeLegal.PortariaInternaCodigoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.portaria_interna_codigo_invalido", "PortariaInternaCodigo inválido")),
        new("ObrigatoriedadeLegal.VigenciaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.vigencia_invalida", "Vigência inválida")),
        new("ObrigatoriedadeLegal.HashColisao", new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.selecao.obrigatoriedade_legal.hash_colisao", "Colisão de hash de regra ativa")),
        new("ObrigatoriedadeLegal.NaoEncontrada", new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.obrigatoriedade_legal.nao_encontrada", "ObrigatoriedadeLegal não encontrada")),
        // Configuração do Processo Seletivo (Story #758, UNI-REQ-0014/0015) — F0.
        // Invariantes do agregado-raiz nesta fatia: etapas e atendimento
        // especializado (ADR-0067). Vagas/bônus/desempate/classificação entram
        // nas fatias F2–F4 sobre o rol_de_regras, com seus próprios códigos.
        new("ProcessoSeletivo.NaoEncontrado", new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.processo_seletivo.nao_encontrado", "Processo Seletivo não encontrado")),
        new("ProcessoSeletivo.EtapasVazias", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapas_vazias", "O processo deve ter ao menos uma etapa pontuada")),
        new("ProcessoSeletivo.OrdemEtapaDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.ordem_etapa_duplicada", "Ordem de etapa duplicada no processo")),
        new("ProcessoSeletivo.IdEtapaDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.id_etapa_duplicado", "O mesmo Id de etapa foi informado mais de uma vez no payload")),
        new("ProcessoSeletivo.NenhumaEtapaComponeNota", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.nenhuma_etapa_compoe_nota", "Ao menos uma etapa deve compor a nota final")),
        new("ProcessoSeletivo.EtapaReferenciadaPorClassificacao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_referenciada_por_classificacao", "Etapa referenciada por regra de eliminação da classificação")),
        new("OfertaAtendimento.TipoDeficienciaSemCondicaoPcd", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.oferta_atendimento.tipo_deficiencia_sem_condicao_pcd", "Tipo de deficiência só pode ser ofertado sob a condição PcD")),
        new("OfertaAtendimento.CondicaoNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.oferta_atendimento.condicao_nao_encontrada", "Condição de atendimento não encontrada ou não está mais viva")),
        new("OfertaAtendimento.RecursoNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.oferta_atendimento.recurso_nao_encontrado", "Recurso de acessibilidade não encontrado ou não está mais vivo")),
        new("OfertaAtendimento.TipoDeficienciaNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.oferta_atendimento.tipo_deficiencia_nao_encontrado", "Tipo de deficiência não encontrado ou não está mais vivo")),
        new("OfertaAtendimento.CondicaoDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.oferta_atendimento.condicao_duplicada", "Condição de atendimento duplicada na oferta")),
        new("OfertaAtendimento.RecursoDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.oferta_atendimento.recurso_duplicado", "Recurso de acessibilidade duplicado na oferta")),
        new("OfertaAtendimento.TipoDeficienciaDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.oferta_atendimento.tipo_deficiencia_duplicado", "Tipo de deficiência duplicado na oferta")),
        // rol_de_regras (Story #772, F1) — validação da definição de uma regra
        // do catálogo e da referência tipada que a configuração embute.
        new("RegraCatalogo.CodigoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_catalogo.codigo_obrigatorio", "Código da regra obrigatório")),
        new("RegraCatalogo.VersaoObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_catalogo.versao_obrigatoria", "Versão da regra obrigatória")),
        new("RegraCatalogo.TipoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_catalogo.tipo_obrigatorio", "Tipo da regra obrigatório")),
        new("RegraCatalogo.EsquemaArgsInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_catalogo.esquema_args_invalido", "esquema_args deve ser um objeto JSON")),
        new("RegraCatalogo.InvariantesInvalidas", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_catalogo.invariantes_invalidas", "invariantes deve ser um array JSON")),
        new("RegraCatalogo.BaseLegalObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_catalogo.base_legal_obrigatoria", "Base legal da regra obrigatória")),
        new("ReferenciaRegra.CodigoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_regra.codigo_obrigatorio", "Código da regra referenciada obrigatório")),
        new("ReferenciaRegra.VersaoObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_regra.versao_obrigatoria", "Versão da regra referenciada obrigatória")),
        new("ReferenciaRegra.HashInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_regra.hash_invalido", "Hash da regra referenciada inválido")),
        // Distribuição de vagas (Story #773, F2, modelagem P-A) — vagas do
        // agregado-raiz, coerência de cada modalidade selecionada e o snapshot
        // de referência demográfica.
        new("ProcessoSeletivo.DistribuicaoVagasVazia", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.distribuicao_vagas_vazia", "O processo deve ter ao menos uma distribuição de vagas configurada")),
        new("ProcessoSeletivo.OfertaCursoDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.oferta_curso_duplicada", "Cada oferta de curso só pode ter uma distribuição de vagas no processo")),
        new("ConfiguracaoDistribuicaoVagas.VoBaseInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.vo_base_invalido", "VO_base deve ser maior que zero")),
        new("ConfiguracaoDistribuicaoVagas.PrForaDoLimite", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.pr_fora_do_limite", "PR deve estar entre 0,5 e 1,0 (art. 10, II)")),
        new("ConfiguracaoDistribuicaoVagas.ModalidadesVazias", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.modalidades_vazias", "A oferta deve ter ao menos uma modalidade selecionada")),
        new("ConfiguracaoDistribuicaoVagas.ModalidadeDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.modalidade_duplicada", "Cada modalidade só pode ser selecionada uma vez por oferta")),
        new("ConfiguracaoDistribuicaoVagas.ComposicaoOrigemNaoSelecionada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.composicao_origem_nao_selecionada", "A origem da composição RETIRA_DE não está selecionada nesta oferta")),
        new("ConfiguracaoDistribuicaoVagas.RemanejamentoDestinoNaoSelecionado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.remanejamento_destino_nao_selecionado", "O destino do remanejamento DESTINO_UNICO não está selecionado nesta oferta")),
        new("ConfiguracaoDistribuicaoVagas.RemanejamentoParNaoSelecionado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.remanejamento_par_nao_selecionado", "O par do remanejamento CRUZADO não está selecionado nesta oferta")),
        new("ConfiguracaoDistribuicaoVagas.RemanejamentoFallbackNaoSelecionado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.remanejamento_fallback_nao_selecionado", "O fallback do remanejamento CRUZADO não está selecionado nesta oferta")),
        new("ConfiguracaoDistribuicaoVagas.ReferenciaDemograficaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.referencia_demografica_obrigatoria", "A distribuição pela Lei 12.711 exige a referência de reserva demográfica")),
        new("ConfiguracaoDistribuicaoVagas.ModalidadesFederaisIncompletas", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.modalidades_federais_incompletas", "A distribuição pela Lei 12.711 exige as 8 modalidades federais e AC")),
        new("ConfiguracaoDistribuicaoVagas.ReferenciaDemograficaIndevida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.referencia_demografica_indevida", "A referência de reserva demográfica só se aplica à distribuição pela Lei 12.711")),
        new("ConfiguracaoDistribuicaoVagas.OfertaCursoNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.oferta_curso_nao_encontrada", "Oferta de curso não encontrada ou não está mais viva")),
        new("ConfiguracaoDistribuicaoVagas.RegraDistribuicaoNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.regra_distribuicao_nao_encontrada", "Regra de distribuição não encontrada no rol_de_regras")),
        new("ConfiguracaoDistribuicaoVagas.RegraDistribuicaoTipoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.regra_distribuicao_tipo_invalido", "A regra referenciada não é do tipo regra_distribuicao_vagas")),
        new("ConfiguracaoDistribuicaoVagas.ReferenciaDemograficaNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.referencia_demografica_nao_encontrada", "Referência de reserva demográfica não encontrada ou não está mais viva")),
        new("ConfiguracaoDistribuicaoVagas.ModalidadeNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.modalidade_nao_encontrada", "Modalidade não encontrada ou não está mais viva")),
        new("ModalidadeSelecionada.CodigoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.codigo_obrigatorio", "Código da modalidade é obrigatório")),
        new("ModalidadeSelecionada.NaturezaLegalObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.natureza_legal_obrigatoria", "Natureza legal da modalidade é obrigatória")),
        new("ModalidadeSelecionada.ComposicaoVagasObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.composicao_vagas_obrigatoria", "Composição de vagas da modalidade é obrigatória")),
        new("ModalidadeSelecionada.BaseLegalObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.base_legal_obrigatoria", "Base legal da modalidade é obrigatória")),
        new("ModalidadeSelecionada.ComposicaoOrigemObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.composicao_origem_obrigatoria", "Modalidade com composição RETIRA_DE exige o código de origem")),
        new("ModalidadeSelecionada.ComposicaoOrigemIndevida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.composicao_origem_indevida", "Código de origem só se aplica à composição RETIRA_DE")),
        new("ModalidadeSelecionada.CotaReservadaExigeCascata", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.cota_reservada_exige_cascata", "Modalidade de cota reservada deve seguir a cascata legal")),
        new("ModalidadeSelecionada.RemanejamentoDestinoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.remanejamento_destino_obrigatorio", "Modalidade com remanejamento DESTINO_UNICO exige o destino")),
        new("ModalidadeSelecionada.RemanejamentoCruzadoIncompleto", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.remanejamento_cruzado_incompleto", "Modalidade com remanejamento CRUZADO exige par e fallback")),
        new("ModalidadeSelecionada.RemanejamentoDestinoIndevido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.remanejamento_destino_indevido", "Destino de remanejamento só se aplica à regra DESTINO_UNICO")),
        new("ModalidadeSelecionada.RemanejamentoCruzadoIndevido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.modalidade_selecionada.remanejamento_cruzado_indevido", "Par/fallback de remanejamento só se aplicam à regra CRUZADO")),
        new("ReferenciaReservaDemograficaSnapshot.CensoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_reserva_demografica_snapshot.censo_obrigatorio", "Censo de referência é obrigatório")),
        new("ReferenciaReservaDemograficaSnapshot.PercentualInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_reserva_demografica_snapshot.percentual_invalido", "Os percentuais demográficos devem estar entre 0 e 100")),
        new("ReferenciaReservaDemograficaSnapshot.BaseLegalObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_reserva_demografica_snapshot.base_legal_obrigatoria", "Base legal é obrigatória")),
        // Desempate + Bônus (Story #774, F3, modelagem P-B §2.5/§2.6) — RN05
        // (bônus toggle por presença) e INV-B6 (etapa_ref executável).
        new("ProcessoSeletivo.OrdemDesempateDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.ordem_desempate_duplicada", "Cada critério de desempate deve ter uma ordem única dentro do processo")),
        new("ProcessoSeletivo.EtapaRefDesempateInexistente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_ref_desempate_inexistente", "O critério de desempate referencia uma etapa que não existe neste processo")),
        new("ProcessoSeletivo.EtapaReferenciadaPorDesempate", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_referenciada_por_desempate", "A etapa é referenciada por um critério de desempate e não pode ser removida sem antes reconfigurar o desempate")),
        new("CriterioDesempate.OrdemInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.criterio_desempate.ordem_invalida", "A ordem do critério de desempate deve ser maior que zero")),
        new("CriterioDesempate.ArgsIncompativeisComRegra", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.criterio_desempate.args_incompativeis_com_regra", "Os args informados não correspondem à regra referenciada")),
        new("CriterioDesempate.IdadeMinimaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.criterio_desempate.idade_minima_invalida", "A idade mínima do critério IDOSO deve ser maior que zero")),
        new("CriterioDesempate.RegraNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.criterio_desempate.regra_nao_encontrada", "Regra de desempate não encontrada no rol_de_regras")),
        new("CriterioDesempate.RegraTipoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.criterio_desempate.regra_tipo_invalido", "A regra referenciada não é do tipo criterio_desempate")),
        new("CriterioDesempate.EtapaRefObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.criterio_desempate.etapa_ref_obrigatorio", "EtapaRef é obrigatório para a regra DESEMPATE-MAIOR-NOTA-ETAPA")),
        new("CriterioDesempate.IdadeMinimaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.criterio_desempate.idade_minima_obrigatoria", "IdadeMinima é obrigatória para a regra DESEMPATE-IDOSO")),
        new("CriterioDesempate.PredicadoFatoIncompleto", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.criterio_desempate.predicado_fato_incompleto", "Fato, Operador e Valor são obrigatórios para a regra DESEMPATE-PREDICADO-FATO")),
        new("ConfiguracaoBonusRegional.RegraInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_bonus_regional.regra_invalida", "A regra referenciada não é do código BONUS-MULTIPLICATIVO")),
        new("ConfiguracaoBonusRegional.FatorInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_bonus_regional.fator_invalido", "O fator do bônus deve ser maior que zero")),
        new("ConfiguracaoBonusRegional.TetoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_bonus_regional.teto_invalido", "O teto do bônus, quando informado, deve ser maior que zero")),
        new("ConfiguracaoBonusRegional.CamposObrigatorios", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_bonus_regional.campos_obrigatorios", "RegraVersao e Fator são obrigatórios quando RegraCodigo é informado")),
        new("ConfiguracaoBonusRegional.RegraNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_bonus_regional.regra_nao_encontrada", "Regra de bônus não encontrada no rol_de_regras")),
        new("ConfiguracaoBonusRegional.RegraTipoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_bonus_regional.regra_tipo_invalido", "A regra referenciada não é do tipo regra_bonus")),
        // Classificação (Story #775, F4, modelagem P-B §2.1) — 15º bloco
        // canônico, composição por referência de fórmula/precisão/eliminação/
        // ordem de alocação.
        new("ConfiguracaoClassificacao.NOpcoesInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.n_opcoes_invalido", "O número de opções de curso deve ser 1 ou 2 (RN04)")),
        new("ConfiguracaoClassificacao.ArredondamentoIndevido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.arredondamento_indevido", "Arredondamento local não se aplica quando a classificação é importada")),
        new("ConfiguracaoClassificacao.EliminacaoIndevida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.eliminacao_indevida", "Regras de eliminação não se aplicam quando a classificação é importada")),
        new("ConfiguracaoClassificacao.ArredondamentoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.arredondamento_obrigatorio", "Cálculo local exige regra de arredondamento com casas decimais maior que zero")),
        new("ConfiguracaoClassificacao.RegraNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.regra_nao_encontrada", "Regra não encontrada no rol_de_regras")),
        new("ConfiguracaoClassificacao.RegraTipoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.regra_tipo_invalido", "A regra referenciada não é do tipo esperado")),
        new("ConfiguracaoClassificacao.RegraArredondamentoVersaoObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_classificacao.regra_arredondamento_versao_obrigatoria", "Versão da regra de arredondamento é obrigatória quando o código é informado")),
        new("RegraEliminacao.ArgsIncompativeisComRegra", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_eliminacao.args_incompativeis_com_regra", "Os args informados não correspondem à regra referenciada")),
        new("RegraEliminacao.NotaMinimaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_eliminacao.nota_minima_invalida", "A nota mínima da eliminação deve ser não negativa")),
        new("RegraEliminacao.MinimoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_eliminacao.minimo_invalido", "O mínimo do corte de redação deve ser não negativo")),
        new("RegraEliminacao.RegraNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_eliminacao.regra_nao_encontrada", "Regra de eliminação não encontrada no rol_de_regras")),
        new("RegraEliminacao.RegraTipoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_eliminacao.regra_tipo_invalido", "A regra referenciada não é do tipo regra_eliminacao")),
        new("RegraEliminacao.EtapaRefENotaMinimaObrigatorios", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_eliminacao.etapa_ref_e_nota_minima_obrigatorios", "EtapaRef e NotaMinima são obrigatórios para a regra ELIM-NOTA-MINIMA-ETAPA")),
        new("RegraEliminacao.MinimoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_eliminacao.minimo_obrigatorio", "Minimo é obrigatório para a regra ELIM-CORTE-REDACAO")),
        new("ProcessoSeletivo.EtapaRefEliminacaoInexistente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_ref_eliminacao_inexistente", "A regra de eliminação referencia uma etapa que não existe neste processo")),
        new("ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.eliminacao_enem_fora_de_processo_enem", "A regra de eliminação só se aplica a processo baseado em ENEM (SiSU/PSVR)")),
        // Documento do Edital — upload direto via URL pre-assinada (Story #759, T3
        // #784). ObjetoNaoEncontrado/TamanhoExcedido/ContentTypeInvalido/AssinaturaInvalida
        // são recusas de validação da confirmação (422); NaoEncontrado é 404
        // (registro inexistente ou de outro processo).
        new("DocumentoEdital.NaoEncontrado", new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.documento_edital.nao_encontrado", "Documento do Edital não encontrado")),
        new("DocumentoEdital.StatusInvalidoParaConfirmacao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_edital.status_invalido_para_confirmacao", "Somente um documento pendente pode ser confirmado")),
        new("DocumentoEdital.ObjetoNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_edital.objeto_nao_encontrado", "Objeto ainda não enviado ao storage ou expirado")),
        new("DocumentoEdital.TamanhoExcedido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_edital.tamanho_excedido", "Documento excede o tamanho máximo permitido")),
        new("DocumentoEdital.ContentTypeInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_edital.content_type_invalido", "Documento do Edital deve ser do tipo application/pdf")),
        new("DocumentoEdital.AssinaturaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_edital.assinatura_invalida", "Conteúdo do arquivo não corresponde a um PDF válido")),
        // Publicação do Processo Seletivo — RN08 (Story #759, T4 #785). Todo
        // 422: status dominante do módulo para violação de regra de negócio,
        // inclusive quando a origem física é constraint de banco (ADR-0102).
        new("DadosEdital.DocumentoEditalIdObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.dados_edital.documento_edital_id_obrigatorio", "Referência ao documento do Edital é obrigatória")),
        new("DadosEdital.PeriodoInscricaoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.dados_edital.periodo_inscricao_invalido", "O fim do período de inscrição não pode anteceder o início")),
        new("Edital.ProcessoSeletivoIdObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.edital.processo_seletivo_id_obrigatorio", "O Edital deve estar vinculado a um Processo Seletivo")),
        new("Edital.DataPublicacaoDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.edital.data_publicacao_duplicada", "Já existe um Edital publicado neste processo com a mesma data de publicação")),
        new("Edital.AberturaJaExiste", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.edital.abertura_ja_existe", "Este processo já tem um Edital de abertura publicado")),
        new("Edital.ContratoNaturezaInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.edital.contrato_natureza_invalido", "Abertura não carrega edital retificado nem motivo; retificação exige ambos")),
        new("Edital.EditalRetificadoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.edital.edital_retificado_obrigatorio", "A retificação deve referenciar o Edital anterior")),
        new("Edital.MotivoRetificacaoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.edital.motivo_retificacao_obrigatorio", "O motivo da retificação é obrigatório")),
        // SnapshotPublicacao.Congelar (ADR-0063): entidade forensic — guards
        // de invariante lançam ArgumentException (defesa em profundidade
        // contra erro de programação do caller, nunca alcançável a partir de
        // input do usuário), não DomainError. Sem entradas de registry aqui.
        new("ProcessoSeletivo.TransicaoInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.transicao_invalida", "Só é possível publicar um processo em rascunho")),
        new("ProcessoSeletivo.ConformidadeInsuficiente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.conformidade_insuficiente", "Processo não conforme para publicação")),
        new("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.mutacao_pos_publicacao_bloqueada", "Processo publicado não aceita mutação direta da configuração")),
        new("ProcessoSeletivo.DocumentoNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.documento_nao_encontrado", "Documento do Edital não encontrado ou não pertence a este processo")),
        new("ProcessoSeletivo.DocumentoNaoConfirmado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.documento_nao_confirmado", "Somente um documento confirmado pode ser referenciado na publicação")),
        new("ProcessoSeletivo.EditalRetificadoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.edital_retificado_invalido", "A retificação deve referenciar o Edital vigente deste processo")),
        // Cursor.* codes vivem em Infrastructure.Core/Pagination/PaginationDomainErrorRegistration —
        // capability cross-module, registrada uma única vez via AddCursorPagination().
    ];
}
