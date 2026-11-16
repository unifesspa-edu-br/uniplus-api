namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel sobre o cronograma de fases e etapas, na ordem dos gates.</summary>
internal static class ItensDoCronograma
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(
        EstadoDaConfiguracao estado, ContextoDeContagemDePrazos contexto, TiposDeAtoUnicosPorObjeto tiposDeAtoUnicos, CorretoresDasEtapas corretores)
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(estado.CronogramaFases);
        EtapasDoProcesso etapas = EtapasDoProcesso.Das(estado.Etapas);

        return
        [
            .. DaPublicacao(estado, cronograma, etapas, contexto),
            .. DoCicloRecursal(estado, cronograma, etapas),
            .. DasBancasEDaJanelaDasEtapas(estado, cronograma, etapas, corretores),
            .. ConferenciaDosTiposDeAto.Itens(cronograma, etapas, tiposDeAtoUnicos),
        ];
    }

    private static ItemConformidade[] DaPublicacao(
        EstadoDaConfiguracao estado, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, ContextoDeContagemDePrazos contexto) =>
    [
        EtapasNoCronograma.ItemDaFaseQueAgrupaEtapas(cronograma, etapas),
        EtapasNoCronograma.ItemDaFaseDeclaradaPelaEtapa(cronograma, etapas),
        Item("cronograma_inscricao_propria_sem_fase_de_coleta", "Cronograma: inscrição própria tem fase que coleta inscrição", !PendenciasDoCronograma.HaInscricaoPropriaSemFaseDeColeta(estado)),
        .. cronograma.ItensDaCompletude(contexto.FusoInstitucional),
        Item("cronograma_vagas_sem_fase_que_produz_resultado", "Cronograma: vagas ofertadas têm fase que produz resultado", !PendenciasDoCronograma.HaVagasSemFaseQueProduzResultado(estado)),
    ];

    private static ItemConformidade[] DoCicloRecursal(EstadoDaConfiguracao estado, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
    [
        CicloRecursalDoProcesso.Item(cronograma, etapas),
        cronograma.ItemDaAncoraDoRecurso(),
        etapas.ItemDaAncoraDoRecurso(),
        cronograma.ItemDoRecorteDeCompetencia(),
        Item("cronograma_procedimento_de_recurso", "Cronograma: cada janela de recurso declara o procedimento de revisão de toda decisão que o lugar produz", PendenciasDosProcedimentosDeRecurso.PendenciaDosProcedimentosDeRecurso(estado) is null),
    ];

    private static ItemConformidade[] DasBancasEDaJanelaDasEtapas(
        EstadoDaConfiguracao estado, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, CorretoresDasEtapas corretores) =>
    [
        .. ConferenciaDasBancas.Itens(cronograma, etapas, corretores),
        new ItemConformidade("etapa_correcao_independente_com_dois_corretores", DimensaoConformidade.Etapas, "Etapa de tipo que exige correção independente com regra de consolidação de ao menos dois corretores distintos", PendenciasDaCorrecaoIndependente.PendenciaDaCorrecaoIndependente(estado, corretores) is null),
        EtapasNoCronograma.ItemDaJanelaDaEtapaNaFase(cronograma, etapas),
    ];

    private static ItemConformidade Item(string codigo, string mensagem, bool conforme) =>
        new(codigo, DimensaoConformidade.Cronograma, mensagem, conforme);
}
