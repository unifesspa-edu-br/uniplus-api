namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class ProcessoDoGrafoLidoDeTeste
{
    internal static ProcessoSeletivo Criar(ProcessoSeletivo origem, GrafoConfiguracao grafo)
    {
        ProcessoSeletivo processo = ProcessoSeletivo.Criar(
            origem.Nome, origem.TipoProcesso, origem.OrigemCandidatos,
            origem.UnidadeAdministradoraOrigemId, origem.UnidadeAdministradora,
            grafo.Localidade ?? origem.Localidade, grafo.IdentificadorLegivel ?? origem.IdentificadorLegivel!.Value);
        DefinirCronogramaEOferta(processo, grafo);
        DefinirClassificacao(processo, grafo);
        DefinirFormularios(processo, grafo);
        DefinirExigenciasEIdentificacao(processo, grafo);
        return processo;
    }

    private static void Aceita(Result resultado) =>
        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);

    private static void DefinirCronogramaEOferta(ProcessoSeletivo processo, GrafoConfiguracao grafo)
    {
        Aceita(processo.DefinirCronogramaFases(grafo.CronogramaFases, []));
        Aceita(processo.DefinirEtapas(grafo.Etapas));
        Aceita(processo.DefinirAtendimentoEspecializado(grafo.AtendimentoEspecializado));
        Aceita(processo.DefinirDistribuicaoVagas(grafo.DistribuicaoVagas, FatosDeModalidadeDeTeste.DoCatalogo));
        if (grafo.NumeroDeOpcoesDeCurso is { } numero)
        {
            Aceita(processo.DefinirOpcoesDeCurso(numero));
        }
    }

    private static void DefinirClassificacao(ProcessoSeletivo processo, GrafoConfiguracao grafo)
    {
        Aceita(processo.DefinirClassificacao(grafo.Classificacao));
        Aceita(processo.DefinirCriteriosDesempate(grafo.CriteriosDesempate));
        Aceita(processo.DefinirBonusRegional(grafo.BonusRegional is not null, grafo.BonusRegional));
        Aceita(processo.DefinirCascataRemanejamento(grafo.CascataRemanejamento));
        Aceita(processo.DefinirConfiguracaoDivulgacao(grafo.ConfiguracaoDivulgacao));
        Aceita(processo.DefinirTaxaInscricao(grafo.ConfiguracaoTaxaInscricao));
        Aceita(processo.DefinirRegrasDerivacao(grafo.RegrasDerivacao));
    }

    private static void DefinirFormularios(ProcessoSeletivo processo, GrafoConfiguracao grafo)
    {
        foreach (FormularioProcesso formulario in grafo.Formularios.OrderBy(f => f.Finalidade))
        {
            Aceita(processo.DefinirFormulario(formulario.Finalidade, formulario.FaseId, formulario.Titulo,
                [.. formulario.Etapas], [.. grafo.FatosColetados.Where(f => f.Finalidade == formulario.Finalidade)]));
            DefinirColeta(processo, grafo, formulario.Finalidade);
        }

        foreach (IGrouping<string, OpcaoDeclaradaFato> opcoes in grafo.OpcoesDeclaradas.GroupBy(o => o.FatoCodigo))
        {
            Aceita(processo.DefinirOpcoesDeclaradas(opcoes.Key, [.. opcoes]));
        }
    }

    private static void DefinirColeta(ProcessoSeletivo processo, GrafoConfiguracao grafo, FinalidadeFormulario finalidade)
    {
        Aceita(processo.DefinirFatosColetados(finalidade,
            [.. grafo.FatosColetados.Where(f => f.Finalidade == finalidade)],
            [.. grafo.GruposColetados.Where(g => g.Finalidade == finalidade)]));
        Aceita(processo.DefinirTermosDoFormulario(finalidade, [.. grafo.TermosExigidos.Where(t => t.Finalidade == finalidade)]));
    }

    private static void DefinirExigenciasEIdentificacao(ProcessoSeletivo processo, GrafoConfiguracao grafo)
    {
        Aceita(processo.DefinirReferenciaTemporalFatos(grafo.ReferenciaTemporalFatos));
        Aceita(processo.DefinirDocumentosExigidos([.. grafo.NosExigencia.Where(n => n.NoPaiId is null)]));
        if (grafo.AlgoritmoContagemPrazo is { } algoritmo)
        {
            Aceita(processo.DefinirAlgoritmoContagemPrazo(algoritmo));
        }

        if (grafo.IdentificadorLegivel is null)
        {
            IdentificadoresDeTeste.Retirar(processo);
        }
    }
}
