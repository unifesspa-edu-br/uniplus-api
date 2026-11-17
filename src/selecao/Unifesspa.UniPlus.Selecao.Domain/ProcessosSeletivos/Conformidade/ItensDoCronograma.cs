namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel sobre o cronograma de fases e etapas, na ordem em que a conferência da configuração confere.</summary>
internal static class ItensDoCronograma
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(EstadoDaConfiguracao estado, CadastrosDaConfiguracao cadastros)
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(estado.CronogramaFases);
        EtapasDoProcesso etapas = EtapasDoProcesso.Das(estado.Etapas);

        return
        [
            .. DaPublicacao(estado, cronograma, etapas, cadastros.ContagemDePrazos),
            .. DoCicloRecursal(estado, cronograma, etapas),
            .. DasBancasEDaJanelaDasEtapas(estado, cronograma, etapas, cadastros.Corretores),
            .. ConferenciaDosTiposDeAto.Itens(cronograma, etapas, cadastros.TiposDeAtoUnicos),
        ];
    }

    private static ItemConformidade[] DaPublicacao(
        EstadoDaConfiguracao estado, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, ContextoDeContagemDePrazos contexto) =>
    [
        cronograma.ItemDaExistenciaDeFases(),
        cronograma.ItemDasDeclaracoes(),
        cronograma.ItemDasFasesEmConjunto(),
        etapas.ItemDasDeclaracoes(),
        etapas.ItemDasEtapasEmConjunto(),
        EtapasNoCronograma.ItemDaFaseQueAgrupaEtapas(cronograma, etapas),
        EtapasNoCronograma.ItemDaFaseDeclaradaPelaEtapa(cronograma, etapas),
        cronograma.ItemDaInscricaoPropria(estado.OrigemCandidatos),
        .. cronograma.ItensDaCompletude(contexto.FusoInstitucional),
        cronograma.ItemDasVagasOfertadas(estado.HaVagasOfertadas),
    ];

    private static ItemConformidade[] DoCicloRecursal(EstadoDaConfiguracao estado, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
    [
        CicloRecursalDoProcesso.Item(cronograma, etapas),
        cronograma.ItemDaAncoraDoRecurso(),
        etapas.ItemDaAncoraDoRecurso(),
        cronograma.ItemDoRecorteDeCompetencia(),
        Item("cronograma_procedimento_de_recurso", "Cronograma: cada janela de recurso declara o procedimento de revisão de toda decisão que o lugar produz", PendenciasDosProcedimentosDeRecurso.CausasDosProcedimentosDeRecurso(estado).Count == 0),
    ];

    private static ItemConformidade[] DasBancasEDaJanelaDasEtapas(
        EstadoDaConfiguracao estado, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, CorretoresDasEtapas corretores) =>
    [
        .. ConferenciaDasBancas.Itens(cronograma, etapas, corretores),
        new ItemConformidade("etapa_correcao_independente_com_dois_corretores", DimensaoConformidade.Etapas, "Etapa de tipo que exige correção independente com regra de consolidação de ao menos dois corretores distintos", PendenciasDaCorrecaoIndependente.CausasDaCorrecaoIndependente(estado, corretores).Count == 0),
        EtapasNoCronograma.ItemDaJanelaDaEtapaNaFase(cronograma, etapas),
    ];

    private static ItemConformidade Item(string codigo, string mensagem, bool conforme) =>
        new(codigo, DimensaoConformidade.Cronograma, mensagem, conforme);
}
