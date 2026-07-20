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
        new("ObrigatoriedadeLegal.TipoProcessoCodigoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.tipo_processo_codigo_obrigatorio", "TipoProcessoCodigo obrigatório")),
        new("ObrigatoriedadeLegal.TipoProcessoCodigoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.tipo_processo_codigo_invalido", "TipoProcessoCodigo inválido")),
        new("ObrigatoriedadeLegal.TipoProcessoCodigoForaDoVocabulario", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.tipo_processo_codigo_fora_do_vocabulario", "TipoProcessoCodigo fora do vocabulário")),
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
        new("ProcessoSeletivo.CondicaoAtendimentoReferenciadaPorExigenciaViva", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.condicao_atendimento_referenciada_por_exigencia_viva", "Existe condição de gatilho documental referenciando um código de condição de atendimento que deixaria de ser ofertado")),
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
        // Quadro de vagas (issue #848/ADR-0115) — cálculo do ramo federal (Lei
        // 12.711), fixação do ramo institucional, e a regra de ajuste (art. 11
        // §único) congelada junto da configuração.
        new("ConfiguracaoDistribuicaoVagas.QuadroAmplaConcorrenciaNegativa", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.quadro_ampla_concorrencia_negativa", "A ampla concorrência resultaria negativa — ajuste o VO, o PR ou as retiradas")),
        new("ConfiguracaoDistribuicaoVagas.QuantidadeVagaNegativa", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.quantidade_vaga_negativa", "A quantidade de vagas de uma modalidade não pode ser negativa")),
        new("ConfiguracaoDistribuicaoVagas.QuadroChaveColide", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.quadro_chave_colide", "Uma retirada ou suplemento não pode usar o código de uma sub-reserva federal ou da ampla concorrência")),
        new("ConfiguracaoDistribuicaoVagas.RegraAjusteObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.regra_ajuste_obrigatoria", "A distribuição pela Lei 12.711 exige a regra de ajuste (art. 11 §único)")),
        new("ConfiguracaoDistribuicaoVagas.QuadroModalidadeAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.quadro_modalidade_ausente", "Uma modalidade selecionada não tem quantidade fixada pelo edital")),
        new("ConfiguracaoDistribuicaoVagas.QuantidadeCalculadaNaoInformavel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.quantidade_calculada_nao_informavel", "A quantidade de uma modalidade calculada pela Lei 12.711 não pode ser fixada pelo edital")),
        new("ConfiguracaoDistribuicaoVagas.QuantidadeDeclaradaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.quantidade_declarada_obrigatoria", "A modalidade de retirada ou suplemento exige a quantidade fixada pelo edital")),
        new("ConfiguracaoDistribuicaoVagas.RegraAjusteNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.regra_ajuste_nao_encontrada", "Regra de ajuste não encontrada no rol_de_regras")),
        new("ConfiguracaoDistribuicaoVagas.RegraAjusteTipoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.regra_ajuste_tipo_invalido", "A regra referenciada não é do tipo regra_ajuste_distribuicao_vagas")),
        new("ConfiguracaoDistribuicaoVagas.QuadroModalidadeNaoSelecionada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.quadro_modalidade_nao_selecionada", "O quadro traz uma modalidade que não está selecionada nesta oferta")),
        new("ConfiguracaoDistribuicaoVagas.RetiradaFederalDeveSerDeAmplaConcorrencia", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.retirada_federal_deve_ser_de_ampla_concorrencia", "No ramo federal, toda modalidade de retirada só pode retirar da ampla concorrência")),
        new("VagaOfertada.ModalidadeCodigoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.vaga_ofertada.modalidade_codigo_obrigatorio", "Código da modalidade da vaga ofertada é obrigatório")),
        new("ProcessoSeletivo.AcaoQuandoIndeferidoDivergente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.acao_quando_indeferido_divergente", "O mesmo código de modalidade não pode ter ações divergentes de vaga quando indeferido em ofertas distintas do processo")),
        new("ProcessoSeletivo.ModalidadeReferenciadaPorExigenciaViva", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.modalidade_referenciada_por_exigencia_viva", "Existe condição de gatilho documental referenciando um código de modalidade que deixaria de ser ofertado")),
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
        // PredicadoDnf — VO compartilhado de predicado sobre o candidato (Story #847,
        // ADR-0111). Consumido hoje por DESEMPATE-PREDICADO-FATO; consumido amanhã por
        // #554 (CondicaoGatilho) e #559 (CampoFormulario.CondicaoExibicao) sem
        // reimplementar a gramática.
        new("ClausulaDnf.ClausulaVazia", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.clausula_dnf.clausula_vazia", "Uma cláusula do predicado deve ter ao menos uma condição")),
        new("CondicaoDnf.FatoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.condicao_dnf.fato_obrigatorio", "O fato da condição é obrigatório")),
        new("CondicaoDnf.OperadorInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.condicao_dnf.operador_invalido", "O operador da condição não é reconhecido")),
        new("CondicaoDnf.FormaIncoerenteComOperador", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.condicao_dnf.forma_incoerente_com_operador", "A forma do valor não é coerente com o operador da condição")),
        new("PredicadoDnf.FatoDesconhecido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.predicado_dnf.fato_desconhecido", "O fato da condição não pertence ao vocabulário fechado")),
        new("PredicadoDnf.OperadorIncompativelComDominio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.predicado_dnf.operador_incompativel_com_dominio", "O operador da condição não é compatível com o domínio do fato")),
        new("PredicadoDnf.ValorIncompativelComTipo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.predicado_dnf.valor_incompativel_com_tipo", "O valor da condição não é compatível com o tipo do fato")),
        new("PredicadoDnf.ValorForaDoDominio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.predicado_dnf.valor_fora_do_dominio", "O valor da condição não pertence ao domínio declarado do fato")),
        new("PredicadoDnf.FatoNaoColetadoPeloProcesso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.predicado_dnf.fato_nao_coletado_pelo_processo", "O fato da condição não é coletado por este processo")),
        // Story #554, PR #896: extensão do PredicadoDnf para domínio dinâmico/multivalorado.
        new("PredicadoDnf.DominioDinamicoNaoFornecido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.predicado_dnf.dominio_dinamico_nao_fornecido", "O domínio dinâmico do fato não foi fornecido pelo chamador")),
        new("DescritorFatoCandidato.DominioIncoerente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.descritor_fato_candidato.dominio_incoerente", "A combinação de domínio e valores declarados do fato é incoerente")),
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
        new("ProcessoSeletivo.TipoDeAtoSemVersaoVigente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.tipo_de_ato_sem_versao_vigente", "Não há versão vigente do tipo de ato declarado na data de publicação")),
        new("ProcessoSeletivo.TipoDeAtoNaoCongelaConfiguracao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.tipo_de_ato_nao_congela_configuracao", "O ato que cria a versão da configuração precisa ser de um tipo que congela configuração")),
        new("ProcessoSeletivo.ObjetoJaTemAtoVivoDoTipo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.objeto_ja_tem_ato_vivo_do_tipo", "Este Processo Seletivo já tem um ato vivo do tipo declarado, de outra linhagem")),
        new("ProcessoSeletivo.AtoJaRetificado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.ato_ja_retificado", "O ato que esta retificação emendaria já foi retificado — a cadeia de atos é linear")),
        new("ProcessoSeletivo.MotivoRetificacaoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.motivo_retificacao_obrigatorio", "O motivo da retificação é obrigatório")),
        // VersaoConfiguracao (ADR-0104/0063): entidade forensic — os guards de
        // SHAPE (bytes vazios, hash malformado, id zerado) lançam
        // ArgumentException (defesa em profundidade contra erro de programação
        // do caller, nunca alcançável a partir de input do usuário) e não têm
        // entrada aqui. Já as invariantes de NEGÓCIO da cadeia de versões
        // afloram como DomainError → 422, tanto pelo domínio quanto pelos guard
        // rails de banco que fecham a corrida check-then-act (ADR-0102).
        new("VersaoConfiguracao.CadeiaQuebrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.versao_configuracao.cadeia_quebrada", "O ato criador da versão não retifica o ato criador da versão anterior")),
        new("VersaoConfiguracao.AtoCriadorRepetido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.versao_configuracao.ato_criador_repetido", "Um ato congela a configuração no máximo uma vez")),
        new("VersaoConfiguracao.VersaoAnteriorDeOutroProcesso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.versao_configuracao.versao_anterior_de_outro_processo", "A cadeia de versões não atravessa certames")),
        new("VersaoConfiguracao.NumeroDuplicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.versao_configuracao.numero_duplicado", "Outra publicação concorrente já criou esta versão da configuração")),
        new("VersaoConfiguracao.AtoCriadorJaCriouVersao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.versao_configuracao.ato_criador_ja_criou_versao", "O ato informado já criou uma versão da configuração")),
        new("VersaoConfiguracao.NumeracaoComBuraco", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.versao_configuracao.numeracao_com_buraco", "A numeração das versões da configuração é contígua")),
        new("VersaoConfiguracao.ContratoAberturaInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.versao_configuracao.contrato_abertura_invalido", "A versão 1 não retifica ato algum; toda versão seguinte retifica")),
        new("VersaoConfiguracao.VigenciaRegressiva", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.versao_configuracao.vigencia_regressiva", "A vigência de uma versão não pode preceder a da versão anterior")),
        new("ProcessoSeletivo.TransicaoInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.transicao_invalida", "Só é possível publicar um processo em rascunho")),
        new("ProcessoSeletivo.ConformidadeInsuficiente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.conformidade_insuficiente", "Processo não conforme para publicação")),
        new("ProcessoSeletivo.ConformidadeLegalInsuficiente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.conformidade_legal_insuficiente", "Processo não conforme às obrigatoriedades legais vigentes")),
        new("ProcessoSeletivo.MutacaoPosPublicacaoBloqueada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.mutacao_pos_publicacao_bloqueada", "Processo publicado não aceita mutação direta da configuração")),
        new("ProcessoSeletivo.DocumentoNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.documento_nao_encontrado", "Documento do Edital não encontrado ou não pertence a este processo")),
        new("ProcessoSeletivo.DocumentoNaoConfirmado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.documento_nao_confirmado", "Somente um documento confirmado pode ser referenciado na publicação")),
        // Story #919 (RN08): defesa em profundidade — o vocabulário de fatos é fechado e
        // append-only (ADR-0111), então este código não deveria ocorrer em produção, mas
        // precisa de mapeamento nomeado (não um 500 genérico) se ocorrer.
        new("ProcessoSeletivo.FatoCongeladoNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fato_congelado_nao_encontrado", "Fato citado em condição de gatilho não encontrado no catálogo de fatos do candidato")),
        // Seletor de snapshot vigente (T6 #787, ADR-0075/0076): não há publicação
        // vigente ≤ o instante consultado — 422, nunca retorno silencioso.
        new("Snapshot.VigenteAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.snapshot.vigente_ausente", "Nenhuma publicação vigente para o instante")),
        // Reposição da configuração congelada (Story #859, ADR-0110 D2). Todos 422: são
        // regras de negócio, e o operador que dispara um descarte precisa saber por que ele
        // foi recusado — reidratar mal é pior do que não reidratar.
        new("ProcessoSeletivo.RestauracaoForaDePublicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.restauracao_fora_de_publicado", "Só um processo publicado tem configuração congelada a restaurar")),
        new("ProcessoSeletivo.IdEtapaAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.id_etapa_ausente", "Toda etapa restaurada declara o Id congelado no envelope")),
        new("VersaoConfiguracao.VersaoDeOutroProcesso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.versao_configuracao.versao_de_outro_processo", "A configuração congelada de um certame não se repõe em outro")),
        // Cronograma de fases (Story #851, §3.6/§3.7): estrutura, janelas, precedência,
        // piso mínimo derivado da origem dos candidatos/vagas ofertadas e o gate de
        // recurso. Todos 422 (ADR-0102) — a lista dos 19+1 códigos declarados na issue,
        // mais 3 "NaoEncontrada"/"NaoEncontrado" adicionais, no MESMO molde de
        // ConfiguracaoDistribuicaoVagas.OfertaCursoNaoEncontrada/RegraDistribuicaoNaoEncontrada
        // acima, para a resolução cross-módulo de FaseCanonica/TipoBanca/tipo de ato
        // produzido — necessários para o handler recusar um id que não resolve, e que a
        // issue não enumerou por não serem invariantes do §3.6/§3.7 propriamente.
        new("ProcessoSeletivo.CronogramaFasesVazio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.cronograma_fases_vazio", "O processo deve ter ao menos uma fase no cronograma")),
        new("ProcessoSeletivo.OrdemFaseDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.ordem_fase_duplicada", "Cada fase deve ter uma ordem única dentro do cronograma")),
        new("ProcessoSeletivo.FaseCanonicaDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fase_canonica_duplicada", "A mesma fase canônica não pode aparecer duas vezes no cronograma")),
        new("ProcessoSeletivo.PrecedenciaFaseViolada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.precedencia_fase_violada", "A ordem declarada viola a precedência entre fases do cadastro")),
        new("ProcessoSeletivo.SobreposicaoDeJanelasNaoPermitida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.sobreposicao_de_janelas_nao_permitida", "As janelas de duas fases dependentes se sobrepõem, e o cadastro não permite sobreposição")),
        new("ProcessoSeletivo.AvaliacaoSemEtapa", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.avaliacao_sem_etapa", "Há fase que agrupa etapas, mas o processo não tem etapa pontuada")),
        new("ProcessoSeletivo.EtapaSemFaseDeAvaliacao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapa_sem_fase_de_avaliacao", "Há etapa pontuada, mas nenhuma fase do cronograma agrupa etapas")),
        new("ProcessoSeletivo.InscricaoPropriaSemFaseDeColeta", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.inscricao_propria_sem_fase_de_coleta", "A origem dos candidatos é inscrição própria, e nenhuma fase coleta inscrição")),
        new("ProcessoSeletivo.VagasSemFaseQueProduzResultado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.vagas_sem_fase_que_produz_resultado", "Há vagas ofertadas, e nenhuma fase do cronograma produz resultado")),
        new("FaseCronograma.JanelaObrigatoriaEmDataPropria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.janela_obrigatoria_em_data_propria", "Fase com origem de data própria exige início e fim da janela")),
        new("FaseCronograma.JanelaInvertida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.janela_invertida", "O fim da janela da fase não pode anteceder o início")),
        new("FaseCronograma.AtoProduzidoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.ato_produzido_obrigatorio", "Fase que produz resultado precisa declarar o código do ato que produz")),
        new("FaseCronograma.FaseCanonicaNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.fase_canonica_nao_encontrada", "Fase canônica não encontrada ou não está mais viva")),
        new("FaseCronograma.TipoBancaNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.tipo_banca_nao_encontrado", "Tipo de banca não encontrado ou não está mais vivo")),
        new("FaseCronograma.AtoProduzidoNaoEncontradoNoCatalogo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.ato_produzido_nao_encontrado_no_catalogo", "O tipo de ato produzido pela fase não tem versão vigente no catálogo de Publicações")),
        new("RegraRecursoFase.FaseNaoProduzResultado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.fase_nao_produz_resultado", "A fase não produz resultado e não pode admitir regra de recurso")),
        new("RegraRecursoFase.RecursoContraResultadoDefinitivo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.recurso_contra_resultado_definitivo", "Não cabe recurso contra resultado definitivo")),
        new("RegraRecursoFase.AncoraDeOutraFase", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.ancora_de_outra_fase", "O ato recorrido tem de ser o ato produzido pela própria fase")),
        new("RegraRecursoFase.AncoraEmAtoCongelante", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.ancora_em_ato_congelante", "A âncora do recurso nunca é o ato que congela a configuração")),
        new("RegraRecursoFase.AncoraNaoEncontradaNoCatalogo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.ancora_nao_encontrada_no_catalogo", "O tipo de ato âncora não tem versão vigente no catálogo de Publicações")),
        new("RegraRecursoFase.PrazoEmDiasUteisSemCalendario", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.prazo_em_dias_uteis_sem_calendario", "O prazo de interposição em dias úteis é recusado — não há calendário de dias úteis no sistema")),
        new("RegraRecursoFase.SuspensividadeEmDiasUteisSemCalendario", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.suspensividade_em_dias_uteis_sem_calendario", "A suspensividade em dias úteis é recusada — não há calendário de dias úteis no sistema")),
        new("RegraRecursoFase.RegraCatalogoInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.regra_recurso_fase.regra_catalogo_invalida", "A regra referenciada não é RECURSO-PRAZO-ANCORADO-EM-ATO, ou diverge do rol_de_regras")),
        // Codec do envelope (Story #859, ADR-0110 D1/D8). A recusa é NOMEADA: um descarte
        // que falha precisa distinguir uma versão que o sistema não conhece, uma que ele
        // conhece e não reidrata, e uma evidência que não prova o que diz provar.
        new("EnvelopeCodec.VersaoDesconhecida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.versao_desconhecida", "A versão do envelope congelado não está no registro de codecs")),
        new("EnvelopeCodec.VersaoNaoReidratavel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.versao_nao_reidratavel", "A versão do envelope é conhecida, mas não pode ser reidratada")),
        new("EnvelopeCodec.AlgoritmoNaoSuportado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.algoritmo_nao_suportado", "O algoritmo de hash da versão não é o que o codec daquela forma emite")),
        new("EnvelopeCodec.IntegridadeViolada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.integridade_violada", "Os bytes congelados não produzem o hash registrado na versão")),
        new("EnvelopeCodec.EnvelopeMalformado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.envelope_malformado", "O envelope congelado não tem a forma que a versão dele declara")),
        new("EnvelopeCodec.EnvelopeIncoerenteComAVersao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.envelope_incoerente_com_a_versao", "O envelope contradiz a versão que o guarda")),
        new("EnvelopeCodec.BlocosDerivadosIncoerentes", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.blocos_derivados_incoerentes", "Distribuição, modalidades e ofertas não declaram o mesmo conjunto de ofertas de curso")),
        new("EnvelopeCodec.RegraDesconhecida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.regra_desconhecida", "O envelope referencia um código de regra fora do rol conhecido")),
        new("EnvelopeCodec.RoundTripDivergente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.envelope_codec.round_trip_divergente", "A configuração reidratada não reproduz os bytes congelados")),
        // Sessão editorial de retificação (Story #860, ADR-0110 D3/D5). A precondição é a
        // ÚNICA família de erros do módulo que não é 4xx-de-negócio: 412 e 428 são de
        // PROTOCOLO — a operação não chegou a ser tentada. É também por isso que elas são
        // as únicas respostas < 500 que a idempotência NÃO armazena (D6): cachear um 412
        // prenderia por 24h o cliente que apenas releu o ETag e retentou corretamente.
        new("Precondicao.Requerida", new DomainErrorMapping(StatusCodes.Status428PreconditionRequired, "uniplus.selecao.precondicao_requerida", "Há uma retificação em curso — a mutação exige o If-Match com o ETag da sessão")),
        new("Precondicao.Falhou", new DomainErrorMapping(StatusCodes.Status412PreconditionFailed, "uniplus.selecao.precondicao_falhou", "O If-Match não corresponde ao estado atual da sessão editorial")),
        new("Precondicao.Malformada", new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.selecao.precondicao_malformada", "If-Match sintaticamente inválido (RFC 9110 §8.8.3)")),
        new("RascunhoRetificacao.JaAberta", new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.selecao.retificacao_ja_aberta", "Já existe uma retificação em curso neste processo")),
        new("RascunhoRetificacao.NaoAberta", new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.selecao.retificacao_nao_aberta", "Não há retificação em curso neste processo")),
        new("RascunhoRetificacao.MotivoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.retificacao.motivo_obrigatorio", "O motivo da retificação é obrigatório")),
        new("RascunhoRetificacao.MotivoMuitoLongo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.retificacao.motivo_muito_longo", "O motivo da retificação excede o limite de caracteres")),
        // A base do rascunho deixou de ser o topo da cadeia. Hoje é inalcançável por
        // invariante (o atalho recusa com sessão aberta; o fechamento encerra a sessão na
        // mesma transação; o FOR UPDATE serializa) — é guard rail contra o dia em que um
        // caminho novo quebre isso. Restaurar ou emendar a versão ERRADA é o tipo de defeito
        // que o round-trip NÃO pega: os bytes de uma versão sempre batem com ela mesma.
        new("RascunhoRetificacao.BaseDesatualizada", new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.selecao.retificacao_base_desatualizada", "A versão sobre a qual a retificação foi aberta não é mais o topo da cadeia")),
        // O agregado recusa encerrar a sessão sem que a configuração congelada tenha sido
        // reposta — descartar sem repor deixaria o certame servindo a configuração EDITADA
        // como se ela tivesse sido publicada. São 500 de propósito: não é decisão do usuário
        // nem input dele, é erro de PROGRAMAÇÃO (um handler que esqueceu a reposição), e um
        // 422 o disfarçaria de recusa de negócio.
        new("ProcessoSeletivo.DescarteSemRestauracao", new DomainErrorMapping(StatusCodes.Status500InternalServerError, "uniplus.selecao.processo_seletivo.descarte_sem_restauracao", "A sessão editorial não pode ser encerrada sem repor a configuração congelada")),
        new("ProcessoSeletivo.DescarteComVersaoErrada", new DomainErrorMapping(StatusCodes.Status500InternalServerError, "uniplus.selecao.processo_seletivo.descarte_com_versao_errada", "A configuração reposta não é a da versão base da retificação")),
        // A allowlist que falha FECHADA (D4): antes, um processo Encerrado ou Cancelado
        // aceitava mutação da configuração em silêncio — a trava era uma denylist de um
        // elemento só, e todo estado novo nascia mutável por omissão.
        new("ProcessoSeletivo.MutacaoForaDeEstadoEditavel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.mutacao_fora_de_estado_editavel", "O processo não está em um estado que aceite mutação da configuração")),
        // Documentos exigidos (Story #554, issue #547, PR #895) — núcleo: aplicabilidade
        // GERAL/CONDICIONAL, fase, snapshot-copy do tipo de documento e a guarda
        // fail-closed transitória (B-01) enquanto o bloco `documentosExigidos.exigencias`
        // do envelope segue stub (removida na PR #903, issue #548).
        new("DocumentoExigido.AplicabilidadeObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.aplicabilidade_obrigatoria", "A aplicabilidade da exigência documental é obrigatória")),
        new("DocumentoExigido.ConsequenciaIndeferimentoInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.consequencia_indeferimento_invalida", "Consequência de indeferimento fora do domínio conhecido")),
        new("DocumentoExigido.GeralComCondicao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.geral_com_condicao", "Exigência GERAL não pode conviver com condição de gatilho viva")),
        new("DocumentoExigido.FaseNaoPertenceAoProcesso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.fase_nao_pertence_ao_processo", "A fase informada não pertence ao cronograma deste processo")),
        new("DocumentoExigido.CondicionalVaziaDeterminaResultado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.condicional_vazia_determina_resultado", "Exigência CONDICIONAL sem condição viva que determina resultado nunca seria cobrada de ninguém")),
        new("DocumentoExigido.TipoDocumentoNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.tipo_documento_nao_encontrado", "Tipo de documento não encontrado ou não está mais vivo")),
        // Gate de fase (Story #916): uma condição de gatilho não pode citar um fato cujo
        // PontoResolucao é uma fase posterior à fase em que o documento é exigido — os dois
        // erros são diagnósticos distintos (fase do PontoResolucao ausente do cronograma vs.
        // presente, mas posterior), não reaproveitam o mesmo código.
        new("DocumentoExigido.PontoResolucaoForaDoCronograma", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.ponto_resolucao_fora_do_cronograma", "A fase em que o fato citado na condição de gatilho é conhecido não pertence ao cronograma deste processo")),
        new("DocumentoExigido.FatoResolvidoEmFasePosterior", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.fato_resolvido_em_fase_posterior", "O fato citado na condição de gatilho só é conhecido numa fase posterior à fase em que o documento é exigido")),
        // ProcessoSeletivo.ExigenciasDocumentaisNaoMaterializadas (guarda B-01) removido —
        // Story #554, PR #903, issue #548: o bloco deixou de ser stub, o gate real decide.
        new("FaseCronograma.ReferenciadaPorExigenciaViva", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.referenciada_por_exigencia_viva", "A fase removida do cronograma é referenciada por um documento exigido configurado")),
        // Guards backward de fase (Story #554, PR #900, issue #893, CA-04) — complemento ao
        // guard acima: retirar PermiteComplementacao de uma fase referenciada por
        // exigência com consequência PENDENCIA_REENVIO.
        new("FaseCronograma.PendenciaReenvioExigeComplementacao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.pendencia_reenvio_exige_complementacao", "A fase não pode perder PermiteComplementacao — é referenciada por documento exigido com consequência PENDENCIA_REENVIO")),
        // Achado Codex P2 (PR #900, 4ª rodada) — uma permutação cíclica de Ordem entre
        // fases retidas não tem ordem de UPDATE que a resolva num único SaveChanges.
        new("FaseCronograma.PermutacaoDeOrdemNaoSuportada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.fase_cronograma.permutacao_de_ordem_nao_suportada", "A redefinição do cronograma troca a Ordem entre fases já existentes formando um ciclo fechado, que não pode ser persistido em uma única chamada")),
        // Gatilho DNF (Story #554, PR #896, issue #892) — CondicaoGatilho sobre PredicadoDnf,
        // ReferenciaTemporalFatos e a validação de publicação sem fallback silencioso
        // (ADR-0111:235-236, B-03 do plano).
        new("CondicaoGatilho.ClausulaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.condicao_gatilho.clausula_invalida", "O ordinal da cláusula não pode ser negativo")),
        new("ReferenciaTemporalFatos.TipoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_temporal_fatos.tipo_obrigatorio", "O tipo de referência temporal é obrigatório")),
        new("ReferenciaTemporalFatos.DataIncoerenteComTipo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_temporal_fatos.data_incoerente_com_tipo", "A data só é aceita (e é exigida) quando o tipo é DATA_ESPECIFICA")),
        new("ReferenciaTemporalFatos.FaseIncoerenteComTipo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_temporal_fatos.fase_incoerente_com_tipo", "A fase âncora só é aceita (e é exigida) quando o tipo é INICIO_FASE ou FIM_FASE")),
        new("ReferenciaTemporalFatos.FaseNaoPertenceAoProcesso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.referencia_temporal_fatos.fase_nao_pertence_ao_processo", "A fase âncora não pertence ao cronograma deste processo")),
        new("ProcessoSeletivo.ReferenciaTemporalFatosAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.referencia_temporal_fatos_ausente", "Existe gatilho por FAIXA_ETARIA, mas nenhuma referência temporal de fatos foi configurada")),
        new("ProcessoSeletivo.ReferenciaTemporalFatosFaseInexistente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.referencia_temporal_fatos_fase_inexistente", "A fase âncora da referência temporal de fatos não pertence (mais) ao cronograma")),
        new("ProcessoSeletivo.ReferenciaTemporalFatosExtremoAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.referencia_temporal_fatos_extremo_ausente", "A fase âncora da referência temporal de fatos não tem o extremo (início/fim) definido")),
        new("ProcessoSeletivo.ReferenciaTemporalFatosFimInscricaoIndisponivel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.referencia_temporal_fatos_fim_inscricao_indisponivel", "FIM_INSCRICAO exige uma fase que colete inscrição com Fim definido")),
        // Base legal 1:N (Story #554, PR #898, issue #549, ADR-0074) — DocumentoExigidoBaseLegal
        // e o gate de publicação (ValidadorBaseLegalExigencias) aflora pelo
        // ProcessoSeletivo.ConformidadeInsuficiente já registrado acima, sem código novo.
        new("DocumentoExigidoBaseLegal.ReferenciaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido_base_legal.referencia_obrigatoria", "A referência da base legal é obrigatória")),
        new("DocumentoExigidoBaseLegal.AbrangenciaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido_base_legal.abrangencia_obrigatoria", "A abrangência da base legal é obrigatória")),
        new("DocumentoExigidoBaseLegal.StatusObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido_base_legal.status_obrigatorio", "O status da base legal é obrigatório")),
        // Idade máxima de emissão + formato + tamanho (Story #554, PR #900, issue #893) —
        // aviso, não bloqueio de presença (§1); coerência tudo-nulo OU completo do VO
        // IdadeMaximaEmissao e a checagem eager de fase âncora (mesma família estrutural
        // de ReferenciaTemporalFatos, PR #896, mas por exigência, não por processo).
        new("DocumentoExigido.TamanhoMaximoBytesInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.tamanho_maximo_bytes_invalido", "O tamanho máximo em bytes, quando presente, deve ser maior que zero")),
        new("IdadeMaximaEmissao.CamposIncoerentesComAusencia", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.idade_maxima_emissao.campos_incoerentes_com_ausencia", "Data e a fase âncora só são aceitas quando a idade máxima de emissão está definida")),
        new("IdadeMaximaEmissao.CamposIncompletos", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.idade_maxima_emissao.campos_incompletos", "Valor, Unidade e ReferenciaTipo devem estar todos presentes, ou todos ausentes")),
        new("IdadeMaximaEmissao.ValorInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.idade_maxima_emissao.valor_invalido", "O valor da idade máxima de emissão deve ser maior que zero")),
        new("IdadeMaximaEmissao.DataIncoerenteComTipo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.idade_maxima_emissao.data_incoerente_com_tipo", "A data só é aceita (e é exigida) quando o tipo é DATA_ESPECIFICA")),
        new("IdadeMaximaEmissao.FaseIncoerenteComTipo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.idade_maxima_emissao.fase_incoerente_com_tipo", "A fase âncora só é aceita (e é exigida) quando o tipo é INICIO_FASE ou FIM_FASE")),
        new("IdadeMaximaEmissao.FaseNaoPertenceAoProcesso", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.idade_maxima_emissao.fase_nao_pertence_ao_processo", "A fase âncora da idade máxima de emissão não pertence ao cronograma deste processo")),
        new("IdadeMaximaEmissao.FaseExtremoAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.idade_maxima_emissao.fase_extremo_ausente", "A fase âncora da idade máxima de emissão não tem o extremo (início/fim) definido")),
        // Formatos permitidos (Story #918): substitui o campo singular FormatoPermitido? —
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
        // Coerência consequência↔ação da vaga (Story #554, PR #903, issue #548, CA-05) —
        // ProcessoSeletivo.PendenciaDeCoerenciaDaConsequenciaDeIndeferimento, chamado em
        // Publicar/Retificar/FecharRetificacao logo após a guarda B-01 removida.
        new("DocumentoExigido.RemoveVantagemSemVantagemViva", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.remove_vantagem_sem_vantagem_viva", "A exigência declara REMOVE_VANTAGEM, mas o processo não tem nenhuma vantagem viva para remover")),
        new("DocumentoExigido.ConsequenciaIncoerenteComAcaoDaVaga", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.consequencia_incoerente_com_acao_da_vaga", "A consequência de indeferimento da exigência é incoerente com a ação de indeferimento da modalidade que ela alcança")),
        // Gate PENDENCIA_REENVIO×PermiteComplementacao FORWARD (Story #920) — complementa
        // FaseCronograma.PendenciaReenvioExigeComplementacao (reverso, acima): aqui a escrita
        // da própria exigência/grupo é recusada quando a fase já não permite complementação.
        new("DocumentoExigido.PendenciaReenvioExigeComplementacao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.documento_exigido.pendencia_reenvio_exige_complementacao", "A fase não permite complementação — consequência PENDENCIA_REENVIO exige PermiteComplementacao")),
        // Árvore de satisfação (Story #920, change documentos-exigidos-composicao) —
        // substitui o grupo de satisfação plano (GrupoSatisfacaoId, residual) por nós
        // folha/grupo E/OU. Invariantes de NoExigencia.CriarGrupo + erros de forma do handler
        // (tipo de nó desconhecido, folha sem documento).
        new("NoExigencia.OrdemInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.ordem_invalida", "A ordem do nó não pode ser negativa")),
        new("NoExigencia.GrupoVazio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.grupo_vazio", "Um grupo E/OU não pode ter zero filhos")),
        new("NoExigencia.ArvoreComCiclo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.arvore_com_ciclo", "A árvore de satisfação não pode conter ciclos")),
        new("NoExigencia.GrupoComFasesDiferentes", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.grupo_com_fases_diferentes", "Todos os nós de um grupo precisam pertencer à mesma fase do cronograma")),
        new("NoExigencia.QuantidadeMinimaProibidaEmGrupoE", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.quantidade_minima_proibida_em_grupo_e", "Um grupo E não tem cardinalidade própria — quantidadeMinima é proibida")),
        new("NoExigencia.ConsequenciaProibidaEmGrupoE", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.consequencia_proibida_em_grupo_e", "Um grupo E é transparente e não carrega consequência própria")),
        new("NoExigencia.BaseLegalProibidaEmGrupoE", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.base_legal_proibida_em_grupo_e", "Um grupo E é transparente e não carrega base legal própria")),
        new("NoExigencia.QuantidadeMinimaForaDosLimites", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.quantidade_minima_fora_dos_limites", "quantidadeMinima de um grupo OU/N-de deve estar entre 1 e o número de filhos")),
        new("NoExigencia.ConsequenciaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.consequencia_invalida", "Consequência do grupo fora do domínio conhecido")),
        new("NoExigencia.BaseLegalSemConsequencia", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.base_legal_sem_consequencia", "Base legal de grupo só é permitida quando o grupo carrega consequência")),
        new("NoExigencia.DocumentoObrigatorioEmFolha", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.documento_obrigatorio_em_folha", "Um nó do tipo FOLHA precisa declarar 'documento'")),
        new("NoExigencia.TipoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.tipo_invalido", "Tipo de nó inválido — esperado FOLHA, E ou OU")),
        // Cardinalidade qualificada de folha (Story #921) — NoExigencia.CriarFolha.
        new("NoExigencia.QuantidadeMinimaDeFolhaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.quantidade_minima_de_folha_invalida", "quantidadeMinima de uma folha, quando presente, deve ser maior ou igual a 1")),
        new("NoExigencia.DataReferenciaIndevidaParaChave", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.data_referencia_indevida_para_chave", "dataReferencia não é aceita para a chaveDistincao informada")),
        new("NoExigencia.OcorrenciasEsperadasIndevidasParaChave", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.ocorrencias_esperadas_indevidas_para_chave", "ocorrenciasEsperadas só é aceita quando chaveDistincao é OCORRENCIA")),
        new("NoExigencia.DataReferenciaObrigatoriaParaChaveCalendario", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.data_referencia_obrigatoria_para_chave_calendario", "dataReferencia é obrigatória quando chaveDistincao é COMPETENCIA_MENSAL ou EXERCICIO_ANUAL")),
        new("NoExigencia.OcorrenciasEsperadasVazia", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.ocorrencias_esperadas_vazia", "ocorrenciasEsperadas, quando presente, não pode ser vazia")),
        new("NoExigencia.OcorrenciasEsperadasComIdVazio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.ocorrencias_esperadas_com_id_vazio", "Os identificadores de ocorrenciasEsperadas não podem ser vazios ou em branco")),
        new("NoExigencia.OcorrenciasEsperadasComIdsDuplicados", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.ocorrencias_esperadas_com_ids_duplicados", "Os identificadores de ocorrenciasEsperadas devem ser únicos")),
        new("NoExigencia.OcorrenciasEsperadasQuantidadeMinimaDivergente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.ocorrencias_esperadas_quantidade_minima_divergente", "quantidadeMinima deve ser igual ao número de ocorrenciasEsperadas")),
        new("NoExigencia.ChaveDistincaoInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.chave_distincao_invalida", "chaveDistincao fora do catálogo fechado")),
        new("NoExigencia.QuantidadeMinimaExcedeJanelaRepresentavel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.quantidade_minima_excede_janela_representavel", "quantidadeMinima excede o calendário representável a partir de dataReferencia")),
        // Coerência consequência↔ação da vaga, estendida ao grupo OU/N-de (Story #920) —
        // mesmos dois gates de DocumentoExigido acima, mesmo ponto de chamada.
        new("NoExigencia.RemoveVantagemSemVantagemViva", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.remove_vantagem_sem_vantagem_viva", "O grupo declara REMOVE_VANTAGEM, mas o processo não tem nenhuma vantagem viva para remover")),
        new("NoExigencia.ConsequenciaIncoerenteComAcaoDaVaga", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.consequencia_incoerente_com_acao_da_vaga", "A consequência do grupo é incoerente com a ação de indeferimento da modalidade que ele alcança")),
        // Fail-closed explícito e temporário (Story #920, PR 1/4): o wrapper de árvore no
        // envelope chega na PR 4/4 (snapshot conjunto final) — publicar árvore com grupo
        // E/OU hoje é recusado, não perde dado silenciosamente.
        new("NoExigencia.SnapshotConjuntoAindaNaoSuportado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia.snapshot_conjunto_ainda_nao_suportado", "A árvore de satisfação com grupos E/OU ainda não é publicável — chega na Story seguinte da change")),
        // Base legal 1:N PRÓPRIA de grupo OU/N-de (Story #920) — mesmo shape/mensagens de
        // DocumentoExigidoBaseLegal acima.
        new("NoExigenciaBaseLegal.ReferenciaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia_base_legal.referencia_obrigatoria", "A referência da base legal é obrigatória")),
        new("NoExigenciaBaseLegal.AbrangenciaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia_base_legal.abrangencia_obrigatoria", "A abrangência da base legal é obrigatória")),
        new("NoExigenciaBaseLegal.StatusObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.no_exigencia_base_legal.status_obrigatorio", "O status da base legal é obrigatório")),
        // Resolvedor da árvore de satisfação (Story #920; substitui ResolvedorExigencias.* do
        // grupo plano) — domain service puro, sem caller HTTP nesta Story (o runtime de
        // coleta é fora de escopo); registrado por disciplina uniforme com o resto do módulo.
        new("ResolvedorArvoreSatisfacao.ArvoreAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.resolvedor_arvore_satisfacao.arvore_ausente", "Não há versão vigente congelada para resolver a árvore de exigências documentais")),
        new("ResolvedorArvoreSatisfacao.ArvoreEstruturalmenteInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.resolvedor_arvore_satisfacao.arvore_estruturalmente_invalida", "A árvore de exigências congelada tem identidade repetida entre folhas")),
        // Cursor.* codes vivem em Infrastructure.Core/Pagination/PaginationDomainErrorRegistration —
        // capability cross-module, registrada uma única vez via AddCursorPagination().
    ];
}
