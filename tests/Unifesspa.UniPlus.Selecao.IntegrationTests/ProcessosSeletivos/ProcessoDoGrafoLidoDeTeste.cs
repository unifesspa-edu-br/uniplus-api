namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
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
        Aceita(processo.DefinirBonusRegional(grafo.Vagas.BonusRegional is not null, grafo.Vagas.BonusRegional));
        Aceita(processo.DefinirCascataRemanejamento(grafo.Vagas.Cascata));
        Aceita(processo.DefinirConfiguracaoDivulgacao(grafo.DivulgacaoETaxa.ConfiguracaoDivulgacao));
        Aceita(processo.DefinirTaxaInscricao(grafo.DivulgacaoETaxa.ConfiguracaoTaxaInscricao));
        Aceita(processo.DefinirRegrasDerivacao(grafo.Coleta.RegrasDerivacao));
    }

    private static void DefinirFormularios(ProcessoSeletivo processo, ConfiguracaoPublicada grafo)
    {
        foreach (FormularioProcesso formulario in grafo.Coleta.Formularios.OrderBy(f => f.Finalidade))
        {
            Aceita(processo.DefinirFormulario(formulario.Finalidade, formulario.FaseId, formulario.Titulo,
                [.. formulario.Etapas], [.. grafo.Coleta.Itens.Where(f => f.Finalidade == formulario.Finalidade)]));
            DefinirColeta(processo, grafo, formulario.Finalidade);
        }

        foreach (IGrouping<string, OpcaoDeclaradaFato> opcoes in grafo.Coleta.OpcoesDeclaradas.GroupBy(o => o.FatoCodigo))
        {
            Aceita(processo.DefinirOpcoesDeclaradas(opcoes.Key, [.. opcoes]));
        }
    }

    private static void DefinirColeta(ProcessoSeletivo processo, ConfiguracaoPublicada grafo, FinalidadeFormulario finalidade)
    {
        Aceita(processo.DefinirFatosColetados(finalidade,
            [.. grafo.Coleta.Itens.Where(f => f.Finalidade == finalidade)],
            [.. grafo.Coleta.GruposColetados.Where(g => g.Finalidade == finalidade)]));
        Aceita(processo.DefinirTermosDoFormulario(finalidade, [.. grafo.Coleta.TermosExigidos.Where(t => t.Finalidade == finalidade)]));
    }

    private static void DefinirExigenciasEIdentificacao(ProcessoSeletivo processo, ConfiguracaoPublicada grafo)
    {
        Aceita(processo.DefinirReferenciaTemporalFatos(grafo.Coleta.ReferenciaTemporalFatos));
        Aceita(processo.DefinirDocumentosExigidos([.. grafo.Exigencias.NosExigencia.Where(n => n.NoPaiId is null)]));
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
