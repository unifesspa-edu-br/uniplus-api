namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Runtime.CompilerServices;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class ProcessoDoGrafoLidoDeTeste
{
    internal static ProcessoSeletivo Criar(ProcessoSeletivo origem, ConfiguracaoPublicada grafo)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            origem.Nome, origem.TipoProcesso, origem.OrigemCandidatos,
            origem.UnidadeAdministradoraOrigemId, origem.UnidadeAdministradora,
            grafo.Cronograma.Localidade ?? origem.Localidade, grafo.IdentificadorLegivel ?? origem.IdentificadorLegivel!.Value);
        DefinirCronogramaEOferta(processo, grafo);
        DefinirClassificacao(processo, grafo);
        DefinirFormularios(processo, grafo);
        DefinirExigenciasEIdentificacao(processo, grafo);
        return processo;
    }

    private static void Aceita(Result resultado) =>
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);

    private static void DefinirCronogramaEOferta(ProcessoSeletivo processo, ConfiguracaoPublicada grafo)
    {
        Aceita(processo.DefinirCronogramaFases(grafo.Cronograma.CronogramaFases, []));
        Aceita(processo.DefinirEtapas(grafo.Avaliacao.Etapas));
        Aceita(processo.DefinirAtendimentoEspecializado(grafo.Vagas.AtendimentoEspecializado!));
        Aceita(processo.DefinirDistribuicaoVagas(grafo.Vagas.DistribuicaoVagas, FatosDeModalidadeDeTeste.DoCatalogo));
        if (grafo.Vagas.NumeroDeOpcoesDeCurso is { } numero)
        {
            Aceita(processo.DefinirOpcoesDeCurso(numero));
        }
    }

    private static void DefinirClassificacao(ProcessoSeletivo processo, ConfiguracaoPublicada grafo)
    {
        Aceita(processo.DefinirClassificacao(grafo.Avaliacao.Classificacao!));
        Aceita(processo.DefinirCriteriosDesempate(grafo.Avaliacao.CriteriosDesempate));
        Aceita(processo.DefinirFada(grafo.Vagas.Fada is not null, grafo.Vagas.Fada));
        Aceita(processo.DefinirCascataRemanejamento(grafo.Vagas.Cascata));
        Aceita(processo.DefinirConfiguracaoDivulgacao(grafo.DivulgacaoETaxa.ConfiguracaoDivulgacao));
        Aceita(processo.DefinirTaxaInscricao(grafo.DivulgacaoETaxa.ConfiguracaoTaxaInscricao));
        Aceita(processo.DefinirRegrasDerivacao(grafo.Coleta.RegrasDerivacao));
    }

    // Cada formulário entra depois dos de fase anterior, porque a regra de um formulário só cita o que um anterior já coleta.
    // A coleta de cada um é separada antes de definir qualquer formulário: ao entrar no processo, o item muda o formulário a que pertence.
    private static void DefinirFormularios(ProcessoSeletivo processo, ConfiguracaoPublicada grafo)
    {
        foreach (ColetaDoFormularioLido coleta in ColetasNaOrdemDasFases(grafo))
        {
            Aceita(processo.DefinirFormulario(coleta.Formulario.Declarado, coleta.Formulario.Titulo,
                [.. coleta.Formulario.Paginas], coleta.Itens, coleta.Formulario.ConsequenciaDaFaltaDeEnvio));
            DefinirColeta(processo, coleta);
        }

        foreach (IGrouping<string, OpcaoDeclaradaFato> opcoes in grafo.Coleta.OpcoesDeclaradas.GroupBy(o => o.FatoCodigo))
        {
            Aceita(processo.DefinirOpcoesDeclaradas(opcoes.Key, [.. opcoes]));
        }
    }

    // Ao entrar em um processo, o item muda o formulário a que pertence; por isso a divisão da coleta por formulário
    // é feita uma só vez por grafo, e quem monta outro processo do mesmo grafo reaproveita a divisão.
    private static readonly ConditionalWeakTable<ConfiguracaoPublicada, List<ColetaDoFormularioLido>> ColetasPorGrafo = new();

    private static List<ColetaDoFormularioLido> ColetasNaOrdemDasFases(ConfiguracaoPublicada grafo) =>
        ColetasPorGrafo.GetValue(grafo, DividirColetaPorFormulario);

    private static List<ColetaDoFormularioLido> DividirColetaPorFormulario(ConfiguracaoPublicada grafo) =>
    [
        .. grafo.Coleta.Formularios
            .OrderBy(formulario => OrdemDaFase(grafo, formulario))
            .ThenBy(static formulario => formulario.Codigo, CodigoDoFormulario.OrdemOrdinal)
            .Select(formulario => new ColetaDoFormularioLido(
                formulario,
                [.. grafo.Coleta.Itens.Where(item => item.FormularioId == formulario.Id)],
                [.. grafo.Coleta.GruposColetados.Where(grupo => grupo.FormularioId == formulario.Id)],
                [.. grafo.Coleta.TermosExigidos.Where(termo => termo.FormularioId == formulario.Id)])),
    ];

    private static int OrdemDaFase(ConfiguracaoPublicada grafo, FormularioProcesso formulario) =>
        grafo.Cronograma.CronogramaFases.FirstOrDefault(fase => fase.Id == formulario.FaseId)?.Ordem ?? int.MaxValue;

    private static void DefinirColeta(ProcessoSeletivo processo, ColetaDoFormularioLido coleta)
    {
        Aceita(processo.DefinirFatosColetados(coleta.Formulario.Codigo, coleta.Itens, coleta.Grupos));
        Aceita(processo.DefinirTermosDoFormulario(coleta.Formulario.Codigo, coleta.Termos));
    }

    private sealed record ColetaDoFormularioLido(
        FormularioProcesso Formulario,
        IReadOnlyList<FatoColetado> Itens,
        IReadOnlyList<GrupoColetado> Grupos,
        IReadOnlyList<TermoExigidoFormulario> Termos);

    private static void DefinirExigenciasEIdentificacao(ProcessoSeletivo processo, ConfiguracaoPublicada grafo)
    {
        Aceita(processo.DefinirReferenciaTemporalFatos(grafo.Coleta.ReferenciaTemporalFatos));
        Aceita(processo.DefinirDocumentosExigidos([.. grafo.Exigencias.RegrasDeComprovacao.Where(n => n.RegraQueContemId is null)]));
        if (grafo.Cronograma.AlgoritmoContagemPrazo is { } algoritmo)
        {
            Aceita(processo.DefinirAlgoritmoContagemPrazo(algoritmo));
        }

        if (grafo.IdentificadorLegivel is null)
        {
            IdentificadoresDeTeste.Retirar(processo);
        }
    }
}
