namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cascata;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Desempate;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Prazos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public static class ChecklistDeConformidade
{
    /// <summary>
    /// Os avisos da conformidade: o que o administrador deve confirmar e que nunca impede a
    /// publicação. Lista própria, separada de <see cref="AvaliarConformidade"/>, porque aquela é bicondicional
    /// com os gates e um aviso não tem gate: nenhum aviso recusa uma publicação.
    /// </summary>
    public static IReadOnlyList<AvisoConformidade> AvaliarAvisos(EstadoDaConfiguracao estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        return [
            .. CicloRecursal.DefinitivosEmMaisDeUmLugar(TiposDeAtoDoCronograma.DeclaracoesDeAtos(estado))
                .Select(static repetido => new AvisoConformidade(
                    CodigosDeAvisoDeConformidade.CronogramaTipoDefinitivoEmMaisDeUmLugar,
                    DimensaoConformidade.Cronograma,
                    $"O tipo de ato '{repetido.AtoCodigo}' está declarado como resultado definitivo em mais de uma fase ou etapa ({JuntarLugares(repetido.Lugares)}). Confirme que o edital quer fechar mais de um ciclo com o mesmo tipo.")),
        ];
    }

    internal static string JuntarLugares(IReadOnlyList<string> lugares) =>
        lugares.Count == 1
            ? lugares[0]
            : $"{string.Join(", ", lugares.Take(lugares.Count - 1))} e {lugares[^1]}";

    /// <summary>
    /// Checklist de conformidade ESTRUTURAL do agregado, bicondicional com os gates de congelamento
    /// (<see cref="ProntidaoParaCongelar"/>): todos os itens ficam <see langword="true"/> se e somente se os
    /// gates não têm pendência, então não existe estado em que o checklist declare tudo <c>Ok</c> e a
    /// publicação recuse por razão estrutural.
    /// </summary>
    /// <remarks>
    /// "Estrutural" não é "publicável": com os gates verdes, a publicação ainda pode recusar por
    /// conformidade legal (<c>GET /conformidade-legal</c>), documento confirmado, tipo de ato e outras
    /// leituras que só o handler de publicação avalia (ADR-0109).
    /// <para>
    /// Não há segunda lista de predicados: cada item vem do predicado nomeado que o gate correspondente
    /// também chama. O gate escolhe a primeira falha na precedência fixada pela prontidão; o checklist
    /// projeta todos os vereditos, na ordem dos gates, e uma razão nova de um predicado compartilhado
    /// aparece nos dois ao mesmo tempo.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<ItemConformidade> AvaliarConformidade(
        EstadoDaConfiguracao estado,
        ContextoDeContagemDePrazos contexto, FatosDeModalidade fatosDeModalidade, TiposDeAtoUnicosPorObjeto tiposDeAtoUnicos,
        ProtecaoVigenteDasExigencias? protecao = null)
    {
        ArgumentNullException.ThrowIfNull(estado);

        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(tiposDeAtoUnicos);
        ArgumentNullException.ThrowIfNull(fatosDeModalidade);

        List<RegrasDoDesempate.ViolacaoDoDesempatePorArea> desempate = RegrasDoDesempate.ConferirDesempatePorAreaParaPublicar(estado);

        return
        [
        // Gates anteriores ao agregador genérico, na ordem da prontidão. Têm erro nomeado próprio e
        // por isso não entram nos itens estruturais: lá dentro, a conformidade insuficiente
        // interceptaria a causa específica.
        new ItemConformidade("identificador_legivel_nao_declarado", DimensaoConformidade.Identificacao, "Identificador legível do certame", ContagemDePrazos.PendenciaDoIdentificadorLegivel(estado) is null),
        new ItemConformidade("localidade_nao_declarada", DimensaoConformidade.ContagemDePrazos, "Localidade que rege a contagem dos prazos", ContagemDePrazos.PendenciaDaLocalidade(estado) is null),
        new ItemConformidade("algoritmo_contagem_prazo_nao_declarado", DimensaoConformidade.ContagemDePrazos, "Convenção de contagem dos prazos de recurso", ContagemDePrazos.PendenciaDoAlgoritmoDeContagem(estado) is null),
        new ItemConformidade("calendario_vigente_ausente", DimensaoConformidade.ContagemDePrazos, "Calendário de dias úteis vigente", ContagemDePrazos.PendenciaDoCalendarioVigente(estado, contexto) is null),

        // O fuso não é gate de publicação — zona irresolvível é defeito de instalação, mapeado
        // para 500, e quem publica não tem o que corrigir. Projetá-lo assim mesmo é o que evita
        // que o preflight declare tudo pronto e a publicação devolva um 500 sem explicação.
        new ItemConformidade("fuso_institucional_nao_reconhecido", DimensaoConformidade.ContagemDePrazos, "Fuso institucional reconhecido pelo runtime", contexto.FusoInstitucionalReconhecido),

        .. ItensEstruturaisDeConformidade.Avaliar(estado),

        // ── Cronograma ──
        new ItemConformidade("cronograma_fase_agrupadora_sem_etapa_pontuada", DimensaoConformidade.Cronograma, "Cronograma: fase que agrupa etapas só quando há etapa pontuada", !PendenciasDoCronograma.HaFaseDeAvaliacaoSemEtapa(estado)),
        new ItemConformidade("cronograma_etapa_pontuada_sem_fase_agrupadora", DimensaoConformidade.Cronograma, "Cronograma: etapa pontuada tem fase que agrupa etapas", !PendenciasDoCronograma.HaEtapaSemFaseDeAvaliacao(estado)),
        new ItemConformidade("cronograma_inscricao_propria_sem_fase_de_coleta", DimensaoConformidade.Cronograma, "Cronograma: inscrição própria tem fase que coleta inscrição", !PendenciasDoCronograma.HaInscricaoPropriaSemFaseDeColeta(estado)),
        new ItemConformidade("cronograma_fase_que_coleta_inscricao_sem_janela", DimensaoConformidade.Cronograma, "Cronograma: a fase que coleta inscrição tem início e fim definidos", PendenciasDoCronograma.FaseQueColetaInscricaoSemJanela(estado) is null),
        new ItemConformidade("cronograma_fase_sem_unidade_responsavel", DimensaoConformidade.Cronograma, "Cronograma: toda fase tem a unidade responsável definida", PendenciasDoCronograma.FaseSemUnidadeResponsavel(estado) is null),
        new ItemConformidade("cronograma_janela_de_isencao", DimensaoConformidade.Cronograma, "Cronograma: a janela de solicitação de isenção abre com a inscrição, fecha antes dela e dura cinco dias", PendenciasDoCronograma.JanelaDeIsencaoConforme(estado, contexto)),
        new ItemConformidade("cronograma_vagas_sem_fase_que_produz_resultado", DimensaoConformidade.Cronograma, "Cronograma: vagas ofertadas têm fase que produz resultado", !PendenciasDoCronograma.HaVagasSemFaseQueProduzResultado(estado)),
        new ItemConformidade("cronograma_conclusao_do_ciclo_recursal", DimensaoConformidade.Cronograma, "Cronograma: a fase ou a etapa que publica resultado preliminar tem o definitivo do mesmo tipo de ato, na própria fase ou etapa ou na fase concluinte declarada", CicloRecursalDoCronograma.PendenciaDaConclusaoDoCicloRecursal(estado) is null),
        new ItemConformidade("cronograma_ancora_do_recurso", DimensaoConformidade.Cronograma, "Cronograma: a regra de recurso ancora num produto preliminar da própria fase", CicloRecursalDoCronograma.PendenciaDaAncoraDoRecurso(estado) is null),
        new ItemConformidade("cronograma_ancora_do_recurso_da_etapa", DimensaoConformidade.Cronograma, "Cronograma: cada janela de recurso da etapa ancora num produto preliminar da própria etapa", CicloRecursalDoCronograma.PendenciaDaAncoraDoRecursoDasEtapas(estado) is null),
            new ItemConformidade("cronograma_recorte_de_competencia_das_bancas", DimensaoConformidade.Cronograma, "Cronograma: bancas do mesmo tipo na mesma fase declaram recortes de competência distintos", CicloRecursalDoCronograma.PendenciaDoRecorteDeCompetencia(estado) is null),
            new ItemConformidade("cronograma_etapa_fora_da_janela_da_fase", DimensaoConformidade.Cronograma, "Cronograma: a etapa acontece dentro da janela da fase que a abriga", JanelaDaEtapaNaFase.PendenciaDaJanelaDaEtapaNaFase(estado) is null),
        new ItemConformidade("cronograma_resultado_declarado_com_papel", DimensaoConformidade.Cronograma, "Cronograma: o tipo de ato classificado como resultado declara papel preliminar ou definitivo", TiposDeAtoDoCronograma.PendenciaDoResultadoSemPapel(estado, tiposDeAtoUnicos) is null),
        new ItemConformidade("cronograma_tipo_que_congela_nao_eh_resultado", DimensaoConformidade.Cronograma, "Cronograma: o tipo de ato que congela a configuração não é declarado como item de resultado", TiposDeAtoDoCronograma.PendenciaDoTipoQueCongelaDeclaradoComoResultado(estado, tiposDeAtoUnicos) is null),
        new ItemConformidade("cronograma_tipo_de_ato_unico_declarado_uma_vez", DimensaoConformidade.Cronograma, "Cronograma: o tipo de ato que só sai uma vez por processo é declarado em um único produto", TiposDeAtoDoCronograma.PendenciaDoTipoDeAtoUnico(estado, tiposDeAtoUnicos) is null),

        // ── Cascata de remanejamento (UNI-REQ-0056): o agregado e o detalhamento por razão ──
        new ItemConformidade("cascata_pendente", DimensaoConformidade.CascataRemanejamento, "Cascata de remanejamento", CoberturaDaCascata.PendenciaDaCascata(estado) is null),
        new ItemConformidade("cascata_modalidade_fora_do_regime_federal", DimensaoConformidade.CascataRemanejamento, "Cascata: modalidade SegueCascata usa a regra de distribuição federal", !CoberturaDaCascata.ExisteCascataForaDoRegimeFederal(estado)),
        new ItemConformidade("cascata_origem_ausente", DimensaoConformidade.CascataRemanejamento, "Cascata: origem SegueCascata declarada na cascata de remanejamento", !CoberturaDaCascata.ExisteCascataOrigemAusente(estado)),
        new ItemConformidade("cascata_fallback_nao_ofertado", DimensaoConformidade.CascataRemanejamento, "Cascata: fallback e destinos resolvíveis nas modalidades ofertadas", !CoberturaDaCascata.ExisteCascataFallbackNaoSelecionadoNaOferta(estado)),
        new ItemConformidade("cascata_origem_nao_segue_cascata", DimensaoConformidade.CascataRemanejamento, "Cascata: origem declarada corresponde a modalidade SegueCascata ofertada", !CoberturaDaCascata.ExisteCascataOrigemNaoSegueCascata(estado)),
        new ItemConformidade("cascata_destino_desconhecido", DimensaoConformidade.CascataRemanejamento, "Cascata: destino declarado corresponde a modalidade ofertada", !CoberturaDaCascata.ExisteCascataDestinoDesconhecido(estado)),

        // ── Pré-canonicalização, na mesma ordem do gate ──
        new ItemConformidade("exigencia_condicional_vazia_determina_resultado", DimensaoConformidade.ExigenciasDocumentais, "Exigência documental: sem CONDICIONAL vazia que determina resultado", PendenciasDasExigencias.PendenciaDasExigenciasDocumentais(estado) is null),
        new ItemConformidade("exigencia_finalidade_incoerente_com_a_fase", DimensaoConformidade.ExigenciasDocumentais, "Exigência documental: declara o formulário a que pertence, entre os que a sua fase responde", PendenciasDasExigencias.PendenciaDaFinalidadeDasExigencias(estado) is null),
        new ItemConformidade("exigencia_fato_comprovado_fora_do_processo", DimensaoConformidade.ExigenciasDocumentais, "Exigência documental: comprova só fatos que o processo coleta ou que são derivados congelados na publicação", ProtecaoDasExigencias.PendenciaDoFatoComprovadoForaDoProcesso(estado, protecao) is null),
        new ItemConformidade("exigencia_menos_protegida_que_fato_comprovado", DimensaoConformidade.ExigenciasDocumentais, "Exigência documental: o tipo de documento é tão protegido quanto o mais protegido dos fatos que a exigência comprova", ProtecaoDasExigencias.PendenciaDaProtecaoDasExigencias(estado, protecao) is null),
        new ItemConformidade("exigencia_remove_vantagem_sem_vantagem_viva", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: REMOVE_VANTAGEM com vantagem viva (exigência)", !ConsequenciaDeIndeferimento.ExisteExigenciaRemoveVantagemSemVantagemViva(estado)),
        new ItemConformidade("exigencia_consequencia_incoerente_com_acao_da_vaga", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: coerente com a ação da vaga (exigência)", !ConsequenciaDeIndeferimento.ExisteExigenciaConsequenciaIncoerenteComAcaoDaVaga(estado, fatosDeModalidade)),
        new ItemConformidade("grupo_remove_vantagem_sem_vantagem_viva", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: REMOVE_VANTAGEM com vantagem viva (grupo)", !ConsequenciaDeIndeferimento.ExisteGrupoRemoveVantagemSemVantagemViva(estado)),
        new ItemConformidade("grupo_consequencia_incoerente_com_acao_da_vaga", DimensaoConformidade.ExigenciasDocumentais, "Consequência de indeferimento: coerente com a ação da vaga (grupo)", !ConsequenciaDeIndeferimento.ExisteGrupoConsequenciaIncoerenteComAcaoDaVaga(estado, fatosDeModalidade)),
        new ItemConformidade("referencia_temporal_ausente_com_gatilho_etario", DimensaoConformidade.ColetaDeFatos, "Referência temporal de fatos: configurada quando alguma regra cita a faixa etária", !ReferenciaTemporalDosFatos.ReferenciaTemporalFatosAusenteQuandoExigida(estado)),
        new ItemConformidade("referencia_temporal_fase_fora_do_cronograma", DimensaoConformidade.ColetaDeFatos, "Referência temporal de fatos: fase âncora pertence ao cronograma", !ReferenciaTemporalDosFatos.ReferenciaTemporalFatosFaseNaoPertenceAoCronograma(estado)),
        new ItemConformidade("referencia_temporal_extremo_da_fase_ausente", DimensaoConformidade.ColetaDeFatos, "Referência temporal de fatos: extremo da fase âncora definido", !ReferenciaTemporalDosFatos.ReferenciaTemporalFatosExtremoDaFaseAusente(estado)),
        new ItemConformidade("referencia_temporal_fim_inscricao_indisponivel", DimensaoConformidade.ColetaDeFatos, "Referência temporal de fatos: fase de coleta com Fim definido para FIM_INSCRICAO", !ReferenciaTemporalDosFatos.ReferenciaTemporalFatosFimInscricaoIndisponivel(estado)),
        new ItemConformidade("modalidade_regra_cita_fato_nao_coletado", DimensaoConformidade.DistribuicaoVagas, "Modalidades: a regra de aplicabilidade cita só fatos que o processo coleta ou deriva", PendenciasDosFatos.PendenciaDasRegrasDasModalidades(estado, fatosDeModalidade.MembroPorAgregado) is null),
        new ItemConformidade("derivacao_fatos_citados_inexistentes", DimensaoConformidade.ColetaDeFatos, "Regras de derivação: fatos citados existem no processo", PendenciasDosFatos.PendenciaDeFatosCitados(estado) is null),
        new ItemConformidade("formulario_fato_ausente_de_versao_publicada", DimensaoConformidade.ColetaDeFatos, "Formulários: na retificação, fato de outra finalidade só quando coletado em todas as versões publicadas", CitacoesDosFormularios.PendenciaDeFatoAusenteDeVersaoPublicada(estado) is null),
        new ItemConformidade("formulario_campo_opcional_alimenta_regra", DimensaoConformidade.ColetaDeFatos, "Formulários: campo que alimenta derivação, negação ou impedimento é obrigatório sempre que exibido", PendenciasDosFatos.PendenciaDeCampoOpcionalQueAlimentaRegra(estado) is null),
        new ItemConformidade("exigencia_repete_por_grupo_do_formulario", DimensaoConformidade.ExigenciasDocumentais, "Exigência documental: repete por grupo dos formulários, conhecido até a fase da exigência", PendenciasDasExigencias.PendenciaDaRepeticaoPorGrupo(estado) is null),
        new ItemConformidade("formulario_edicao_enem_sem_classificacao_enem", DimensaoConformidade.ColetaDeFatos, "Formulários: só pedem a edição do ENEM quando a classificação é baseada em ENEM", PendenciasDosFormularios.EdicaoDoEnemSemClassificacaoEnem(estado.Campos, estado.Classificacao) is null),
        new ItemConformidade("fato_coletavel_sem_valores_ofertados", DimensaoConformidade.ColetaDeFatos, "Fato coletável de escopo do processo: oferta declara ao menos um valor", PendenciasDosFatos.PendenciaDeFatoColetadoSemValoresOfertados(estado) is null),
        new ItemConformidade("fato_coletavel_municipio_citado_fora_da_area_do_bonus", DimensaoConformidade.ColetaDeFatos, "Fato com os municípios do bônus regional: condição cita só município da área", PendenciasDosFatos.PendenciaDeMunicipioDoBonusForaDaArea(estado) is null),
        new ItemConformidade("termo_exigido_sem_forma_de_aceite", DimensaoConformidade.ColetaDeFatos, "Termos do formulário: toda versão escolhida tem forma de aceite definida", PendenciasDosFormularios.PendenciaDeTermoSemFormaDeAceite(estado) is null),
        new ItemConformidade("formulario_inscricao_ausente", DimensaoConformidade.ColetaDeFatos, "Formulários: processo com inscrição própria tem formulário de inscrição", PendenciasDosFormularios.PendenciaDoFormularioDeInscricao(estado) is null),
        new ItemConformidade("formulario_inscricao_sem_conjunto_basico", DimensaoConformidade.ColetaDeFatos, "Formulários: o de inscrição coleta o conjunto básico de dados do candidato", PendenciasDosFormularios.PendenciaDoConjuntoBasico(estado) is null),
        new ItemConformidade("formulario_inscricao_sem_edicao_enem", DimensaoConformidade.ColetaDeFatos, "Formulários: com classificação baseada em ENEM, o de inscrição pede a edição do ENEM a todo candidato", PendenciasDosFormularios.PendenciaDaEdicaoDoEnem(estado) is null),
        new ItemConformidade("formulario_fase_incoerente", DimensaoConformidade.ColetaDeFatos, "Formulários: cada um na fase do cronograma que a finalidade pede", PendenciasDosFormularios.PendenciaDaFaseDosFormularios(estado) is null),
        new ItemConformidade("formulario_isencao_sem_taxa", DimensaoConformidade.ColetaDeFatos, "Formulários: isenção de taxa só em processo que cobra taxa", PendenciasDosFormularios.PendenciaDaIsencaoSemTaxa(estado) is null),
        new ItemConformidade("formulario_item_fora_de_secao", DimensaoConformidade.ColetaDeFatos, "Formulários: todo item numa seção, na ordem das seções", PendenciasDosFormularios.PendenciaDosItensForaDeSecao(estado) is null),
        new ItemConformidade("formulario_itens_em_excesso", DimensaoConformidade.ColetaDeFatos, $"Formulários: no máximo {FormaDoItem.MaximoDeItens} itens cada", PendenciasDosFormularios.PendenciaDosItensEmExcesso(estado) is null),
        new ItemConformidade("modalidade_regra_incoerente_com_a_natureza", DimensaoConformidade.DistribuicaoVagas, "Modalidades: a regra de cada cota da lei exige a opção pelas cotas, e a de cada ação afirmativa a recusa", RegrasDaDistribuicaoDeVagas.RegraIncoerenteComANatureza(estado.DistribuicaoVagas) is null),
        new ItemConformidade("modalidade_remanejamento_incoerente_com_a_natureza", DimensaoConformidade.DistribuicaoVagas, "Modalidades: a ampla concorrência não remaneja as vagas ociosas, e cada ação afirmativa as remaneja para um destino único ou para o par", RegrasDaDistribuicaoDeVagas.RemanejamentoIncoerenteComANatureza(estado.DistribuicaoVagas) is null),
        new ItemConformidade("modalidade_cotas_da_lei_diferentes_entre_ofertas", DimensaoConformidade.DistribuicaoVagas, "Modalidades: todos os cursos ofertam as mesmas cotas da lei", PendenciasDaDistribuicaoDeVagas.PendenciaDasCotasDaLeiEntreOfertas(estado) is null),
        new ItemConformidade("modalidade_reserva_pcd_sem_cota_pcd_da_lei", DimensaoConformidade.DistribuicaoVagas, "Modalidades: com cota da lei, a reserva de pessoa com deficiência de ampla concorrência vem com a cota da lei para ela", PendenciasDaDistribuicaoDeVagas.PendenciaDaReservaPcdSemCotaPcdDaLei(estado) is null),
        new ItemConformidade("derivacao_cota_e_acao_afirmativa_juntas", DimensaoConformidade.ColetaDeFatos, "Modalidades: cota da lei e ação afirmativa não se aplicam ao mesmo candidato", ExclusividadeEntreCotaEAcaoAfirmativa.PendenciaDaExclusividadeEntreCotaEAcaoAfirmativa(estado) is null),
        new ItemConformidade("grafo_dependencia_com_ciclo", DimensaoConformidade.ColetaDeFatos, "Grafo de dependência conjunto: sem ciclo", PendenciasDosFatos.PendenciaDoGrafoConjunto(estado) is null),
        new ItemConformidade("criterios_desempate_em_excesso", DimensaoConformidade.Classificacao, $"Critérios de desempate: no máximo {RegrasDoDesempate.CriteriosDesempateMaximo}", RegrasDoDesempate.ConferirQuantidadeDeCriteriosDesempate(estado.CriteriosDesempate.Count).Count == 0),
        new ItemConformidade("desempate_area_enem_areas_mal_formadas", DimensaoConformidade.Classificacao, "Desempate por área do ENEM: cada critério tem ao menos uma área, sem exceder o teto, com códigos bem formados e sem repetição", Sem(desempate, DesempatePorAreaEnemErrorCodes.AreasObrigatorias, DesempatePorAreaEnemErrorCodes.AreasEmExcesso, DesempatePorAreaEnemErrorCodes.AreaInvalida, DesempatePorAreaEnemErrorCodes.AreaRepetida)),
        new ItemConformidade("desempate_area_enem_citada_por_dois_criterios", DimensaoConformidade.Classificacao, "Desempate por área do ENEM: cada área é citada por um critério só", Sem(desempate, DesempatePorAreaEnemErrorCodes.AreaCitadaPorOutroCriterio)),
        .. ItemDoDesempateSemQuadro(estado, desempate),
        new ItemConformidade("desempate_area_enem_fora_do_quadro", DimensaoConformidade.Classificacao, "Desempate por área do ENEM: as áreas citadas estão em todos os grupos do quadro de pesos por área", Sem(desempate, DesempatePorAreaEnemErrorCodes.ForaDoQuadro)),
        ];
    }

    /// <summary>
    /// Omitido quando a classificação calcula pelos pesos por área sem grupo no quadro: a falta
    /// do quadro já é o item da resolução, e aqui sairia verde com uma descrição que o estado
    /// desmente.
    /// </summary>
    internal static ItemConformidade[] ItemDoDesempateSemQuadro(EstadoDaConfiguracao estado, List<RegrasDoDesempate.ViolacaoDoDesempatePorArea> desempate) =>
        estado.Classificacao is { CalculaPelosPesosPorAreaDoEnem: true, TemGrupoNoQuadro: false }
            ? []
            : [new ItemConformidade("desempate_area_enem_sem_quadro", DimensaoConformidade.Classificacao, "Desempate por área do ENEM: a classificação é baseada em ENEM, calculada pela média ponderada, com quadro de pesos por área", Sem(desempate, DesempatePorAreaEnemErrorCodes.SemQuadro))];

    /// <summary>Sem violação dos códigos dados — os itens do checklist separam o que corrigir.</summary>
    internal static bool Sem(List<RegrasDoDesempate.ViolacaoDoDesempatePorArea> violacoes, params string[] codigosDoErro) =>
        !violacoes.Any(v => codigosDoErro.Contains(v.Erro.Code));

    /// <summary>
    /// Pendência de conformidade do processo, ou <see langword="null"/> quando ele está conforme. É a fonte
    /// única do gate: publicar e retificar recusam com o mesmo <c>DomainError</c>.
    /// </summary>
    /// <remarks>
    /// As duas transições abrem uma <see cref="VersaoConfiguracao"/> append-only e juridicamente
    /// vinculante: uma versão congelada a partir de configuração incompleta é irreparável, porque o
    /// passado não se muta.
    /// </remarks>
    public static DomainError? PendenciaDeConformidade(EstadoDaConfiguracao estado)
    {
        ArgumentNullException.ThrowIfNull(estado);

        // Só os itens estruturais entram no agregador genérico: a cascata de remanejamento tem erro
        // nomeado próprio e nunca passa por aqui.
        IReadOnlyList<ItemConformidade> pendencias = [.. ItensEstruturaisDeConformidade.Avaliar(estado).Where(static item => !item.Ok)];
        if (pendencias.Count == 0)
        {
            return null;
        }

        return new DomainError(
            "ProcessoSeletivo.ConformidadeInsuficiente",
            $"Processo não conforme para publicação — pendente: {string.Join(", ", pendencias.Select(static p => p.Mensagem))}.");
    }
}
