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
        new("Edital.NaoEncontrado", new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.edital.nao_encontrado", "Edital não encontrado")),
        new("Edital.JaPublicado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.edital.ja_publicado", "Edital já publicado")),
        new("NumeroEdital.Invalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.numero_edital.invalido", "Número de edital inválido")),
        new("NumeroEdital.AnoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.numero_edital.ano_invalido", "Ano do edital inválido")),
        new("PeriodoInscricao.Invalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.periodo_inscricao.invalido", "Período de inscrição inválido")),
        new("FormulaCalculo.FatorInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formula_calculo.fator_invalido", "Fator de divisão inválido")),
        new("FormulaCalculo.BonusInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formula_calculo.bonus_invalido", "Bônus regional inválido")),
        new("Inscricao.StatusInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.inscricao.status_invalido", "Status de inscrição inválido")),
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
        // Conformidade (Story #461). Snapshot ausente em edital não publicado.
        new("Conformidade.SnapshotNaoDisponivel", new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.conformidade.snapshot_nao_disponivel", "Snapshot de conformidade indisponível — edital não publicado")),
        // Configuração do Processo Seletivo (Story #758, UNI-REQ-0014/0015) — F0.
        // Invariantes do agregado-raiz nesta fatia: etapas e atendimento
        // especializado (ADR-0067). Vagas/bônus/desempate/classificação entram
        // nas fatias F2–F4 sobre o rol_de_regras, com seus próprios códigos.
        new("ProcessoSeletivo.NaoEncontrado", new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.processo_seletivo.nao_encontrado", "Processo Seletivo não encontrado")),
        new("ProcessoSeletivo.EtapasVazias", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.etapas_vazias", "O processo deve ter ao menos uma etapa pontuada")),
        new("ProcessoSeletivo.OrdemEtapaDuplicada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.ordem_etapa_duplicada", "Ordem de etapa duplicada no processo")),
        new("ProcessoSeletivo.NenhumaEtapaComponeNota", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.nenhuma_etapa_compoe_nota", "Ao menos uma etapa deve compor a nota final")),
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
        // Cursor.* codes vivem em Infrastructure.Core/Pagination/PaginationDomainErrorRegistration —
        // capability cross-module, registrada uma única vez via AddCursorPagination().
    ];
}
