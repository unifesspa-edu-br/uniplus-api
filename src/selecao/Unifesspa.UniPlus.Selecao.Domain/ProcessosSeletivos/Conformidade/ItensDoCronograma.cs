namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel sobre o cronograma de fases e etapas, na ordem dos gates.</summary>
internal static class ItensDoCronograma
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(
        EstadoDaConfiguracao estado, ContextoDeContagemDePrazos contexto, TiposDeAtoUnicosPorObjeto tiposDeAtoUnicos) =>
    [
        .. DaEstrutura(estado, contexto),
        .. DoCicloRecursalEDasBancas(estado),
        .. DosTiposDeAto(estado, tiposDeAtoUnicos),
    ];

    private static ItemConformidade[] DaEstrutura(EstadoDaConfiguracao estado, ContextoDeContagemDePrazos contexto) =>
    [
        new ItemConformidade("cronograma_fase_agrupadora_sem_etapa_pontuada", DimensaoConformidade.Cronograma, "Cronograma: fase que agrupa etapas só quando há etapa pontuada", !PendenciasDoCronograma.HaFaseDeAvaliacaoSemEtapa(estado)),
        new ItemConformidade("cronograma_etapa_declara_a_fase", DimensaoConformidade.Cronograma, "Cronograma: toda etapa declara a fase em que acontece", PendenciasDoCronograma.EtapaSemFaseDeclarada(estado) is null),
        new ItemConformidade("cronograma_inscricao_propria_sem_fase_de_coleta", DimensaoConformidade.Cronograma, "Cronograma: inscrição própria tem fase que coleta inscrição", !PendenciasDoCronograma.HaInscricaoPropriaSemFaseDeColeta(estado)),
        new ItemConformidade("cronograma_fase_que_coleta_inscricao_sem_janela", DimensaoConformidade.Cronograma, "Cronograma: a fase que coleta inscrição tem início e fim definidos", PendenciasDoCronograma.FaseQueColetaInscricaoSemJanela(estado) is null),
        new ItemConformidade("cronograma_fase_sem_unidade_responsavel", DimensaoConformidade.Cronograma, "Cronograma: toda fase tem a unidade responsável definida", PendenciasDoCronograma.FaseSemUnidadeResponsavel(estado) is null),
        new ItemConformidade("cronograma_janela_de_isencao", DimensaoConformidade.Cronograma, "Cronograma: a janela de solicitação de isenção abre com a inscrição, fecha antes dela e dura cinco dias", PendenciasDoCronograma.JanelaDeIsencaoConforme(estado, contexto)),
        new ItemConformidade("cronograma_vagas_sem_fase_que_produz_resultado", DimensaoConformidade.Cronograma, "Cronograma: vagas ofertadas têm fase que produz resultado", !PendenciasDoCronograma.HaVagasSemFaseQueProduzResultado(estado)),
    ];

    private static ItemConformidade[] DoCicloRecursalEDasBancas(EstadoDaConfiguracao estado) =>
    [
        new ItemConformidade("cronograma_conclusao_do_ciclo_recursal", DimensaoConformidade.Cronograma, "Cronograma: a fase ou a etapa que publica resultado preliminar tem o definitivo do mesmo tipo de ato, na própria fase ou etapa ou na fase concluinte declarada", CicloRecursalDoCronograma.PendenciaDaConclusaoDoCicloRecursal(estado) is null),
        new ItemConformidade("cronograma_ancora_do_recurso", DimensaoConformidade.Cronograma, "Cronograma: a regra de recurso ancora num produto preliminar da própria fase", CicloRecursalDoCronograma.PendenciaDaAncoraDoRecurso(estado) is null),
        new ItemConformidade("cronograma_ancora_do_recurso_da_etapa", DimensaoConformidade.Cronograma, "Cronograma: cada janela de recurso da etapa ancora num produto preliminar da própria etapa", CicloRecursalDoCronograma.PendenciaDaAncoraDoRecursoDasEtapas(estado) is null),
        new ItemConformidade("cronograma_recorte_de_competencia_das_bancas", DimensaoConformidade.Cronograma, "Cronograma: bancas do mesmo tipo na mesma fase declaram recortes de competência distintos", CicloRecursalDoCronograma.PendenciaDoRecorteDeCompetencia(estado) is null),
        new ItemConformidade("cronograma_etapa_fora_da_janela_da_fase", DimensaoConformidade.Cronograma, "Cronograma: a etapa acontece dentro da janela da fase que a abriga", JanelaDaEtapaNaFase.PendenciaDaJanelaDaEtapaNaFase(estado) is null),
    ];

    private static ItemConformidade[] DosTiposDeAto(EstadoDaConfiguracao estado, TiposDeAtoUnicosPorObjeto tiposDeAtoUnicos) =>
    [
        new ItemConformidade("cronograma_resultado_declarado_com_papel", DimensaoConformidade.Cronograma, "Cronograma: o tipo de ato classificado como resultado declara papel preliminar ou definitivo", TiposDeAtoDoCronograma.PendenciaDoResultadoSemPapel(estado, tiposDeAtoUnicos) is null),
        new ItemConformidade("cronograma_tipo_que_congela_nao_eh_resultado", DimensaoConformidade.Cronograma, "Cronograma: o tipo de ato que congela a configuração não é declarado como item de resultado", TiposDeAtoDoCronograma.PendenciaDoTipoQueCongelaDeclaradoComoResultado(estado, tiposDeAtoUnicos) is null),
        new ItemConformidade("cronograma_tipo_de_ato_unico_declarado_uma_vez", DimensaoConformidade.Cronograma, "Cronograma: o tipo de ato que só sai uma vez por processo é declarado em um único produto", TiposDeAtoDoCronograma.PendenciaDoTipoDeAtoUnico(estado, tiposDeAtoUnicos) is null),
    ];
}
