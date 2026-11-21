namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel sobre o cronograma de fases e etapas, na ordem em que a conferência da configuração confere.</summary>
internal static class ItensDoCronograma
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(ConfiguracaoDeclarada configuracao, CadastrosDaConfiguracao cadastros)
    {
        CronogramaDoProcesso cronograma = CronogramaDoProcesso.Do(configuracao.Cronograma.CronogramaFases, configuracao.Origem);
        EtapasDoProcesso etapas = EtapasDoProcesso.Das(configuracao.Avaliacao.Etapas, configuracao.Origem);

        return
        [
            .. DaPublicacao(configuracao, cronograma, etapas, cadastros.ContagemDePrazos),
            cronograma.ItemDaPrecedenciaEntreFases(cadastros.Precedencias),
            .. DoCicloRecursal(configuracao, cronograma, etapas),
            .. DasBancasEDaJanelaDasEtapas(configuracao, cronograma, etapas, cadastros.Corretores),
            .. ConferenciaDosTiposDeAto.Itens(cronograma, etapas, cadastros.TiposDeAtoUnicos),
        ];
    }

    private static ItemConformidade[] DaPublicacao(
        ConfiguracaoDeclarada configuracao, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, ContextoDeContagemDePrazos contexto) =>
    [
        cronograma.ItemDaExistenciaDeFases(),
        cronograma.ItemDasDeclaracoes(),
        cronograma.ItemDasFasesEmConjunto(),
        etapas.ItemDasDeclaracoes(),
        etapas.ItemDasEtapasEmConjunto(),
        EtapasNoCronograma.ItemDaFaseQueAgrupaEtapas(cronograma, etapas),
        EtapasNoCronograma.ItemDaFaseDeclaradaPelaEtapa(cronograma, etapas),
        cronograma.ItemDaInscricaoPropria(configuracao.Identidade.OrigemCandidatos),
        .. ExigenciaDaHomologacaoPorOrigem.Itens(configuracao.Identidade.OrigemCandidatos, cronograma, etapas),
        .. cronograma.ItensDaCompletude(contexto.FusoInstitucional),
        cronograma.ItemDasVagasOfertadas(configuracao.Vagas.HaVagasOfertadas),
    ];

    private static ItemConformidade[] DoCicloRecursal(ConfiguracaoDeclarada configuracao, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
    [
        CicloRecursalDoProcesso.Item(cronograma, etapas),
        cronograma.ItemDaAncoraDoRecurso(),
        etapas.ItemDaAncoraDoRecurso(),
        cronograma.ItemDoRecorteDeCompetencia(),
        Item("cronograma_procedimento_de_recurso", "Cronograma: cada recurso tem procedimento de revisão para as decisões da fase ou etapa", PendenciasDosProcedimentosDeRecurso.CausasDosProcedimentosDeRecurso(configuracao).Count == 0 && !PendenciasDosProcedimentosDeRecurso.RecusadosPelaLeitura(configuracao)),
    ];

    private static ItemConformidade[] DasBancasEDaJanelaDasEtapas(
        ConfiguracaoDeclarada configuracao, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas, CorretoresDasEtapas corretores) =>
    [
        .. ConferenciaDasBancas.Itens(cronograma, etapas, corretores),
        new ItemConformidade("etapa_correcao_independente_com_dois_corretores", DimensaoConformidade.Etapas, "Etapa de tipo que exige correção independente com regra de consolidação de ao menos dois corretores distintos", PendenciasDaCorrecaoIndependente.CausasDaCorrecaoIndependente(configuracao, corretores).Count == 0),
        EtapasNoCronograma.ItemDaJanelaDaEtapaNaFase(cronograma, etapas),
    ];

    private static ItemConformidade Item(string codigo, string mensagem, bool conforme) =>
        new(codigo, DimensaoConformidade.Cronograma, mensagem, conforme);
}
