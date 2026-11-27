namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConferenciaDaConfiguracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class PropriedadesDeReferenciaDeRegraTests
{
    private static readonly string[] PropriedadesConhecidas =
        [
            "ConfiguracaoCascataRemanejamento.Regra",
            "ConfiguracaoClassificacao.RegraArredondamento",
            "ConfiguracaoClassificacao.RegraCalculo",
            "ConfiguracaoClassificacao.RegraOrdemAlocacao",
            "ConfiguracaoDistribuicaoVagas.RegraAjuste",
            "ConfiguracaoDistribuicaoVagas.RegraDistribuicao",
            "ConfiguracaoDoFada.Regra",
            "CriterioDesempate.Regra",
            "CronogramaDeclarado.AlgoritmoContagemPrazo",
            "DeclaracaoDaAvaliacaoDaEtapa.RegraConsolidacao",
            "DeclaracaoDaClassificacao.RegraArredondamento",
            "DeclaracaoDaClassificacao.RegraCalculo",
            "DeclaracaoDaClassificacao.RegraOrdemAlocacao",
            "DistribuicaoDeVagasDeclarada.RegraDeAjuste",
            "DistribuicaoDeVagasDeclarada.RegraDeDistribuicao",
            "EtapaDeclarada.RegraConsolidacao",
            "EtapaProcesso.RegraConsolidacao",
            "IdentificacaoLida.AlgoritmoDeContagem",
            "ProcedimentoDeRecursoDeclarado.Regra",
            "ProcessoSeletivo.AlgoritmoContagemPrazo",
            "RecursoDaEtapa.Regra",
            "RegraDoCatalogoCitada.Referencia",
            "RegraEliminacao.Regra",
            "RegraRecursoFase.Regra",
            "RegrasDoCatalogo.Arredondamento",
            "RegrasDoCatalogo.Calculo",
            "RegrasDoCatalogo.OrdemDeAlocacao",
        ];

    [Fact]
    public void Referencias_NoDominioDaSelecao_IdentificamTodasAsPropriedadesAConferir()
    {
        string[] propriedades = [.. typeof(ProcessoSeletivo).Assembly.GetTypes()
            .SelectMany(static tipo => tipo.GetProperties()
                .Where(static propriedade => propriedade.PropertyType == typeof(ReferenciaRegra))
                .Select(propriedade => $"{tipo.Name}.{propriedade.Name}"))
            .Order(StringComparer.Ordinal)];

        propriedades.Should().Equal(PropriedadesConhecidas);
    }
}
