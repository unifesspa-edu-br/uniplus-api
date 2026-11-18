namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConferenciaDaConfiguracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class OrdemDasReferenciasDaClassificacaoTests
{
    [Fact]
    public void De_ClassificacaoComEliminacoes_DevolveCamposNaOrdemDeEntrada()
    {
        RegraEliminacao primeira = Eliminacao('a');
        RegraEliminacao segunda = Eliminacao('b');
        ConfiguracaoDeclarada configuracao = Configuracao(primeira, segunda);

        (string Campo, ReferenciaRegra Regra)[] referencias = [.. ReferenciasDeRegraDaConfiguracao.De(configuracao)
            .Where(static item => item.Campo.StartsWith("classificacao", StringComparison.Ordinal))];

        ConferirCamposDaClassificacao(referencias);
        referencias[3].Regra.Should().Be(segunda.Regra);
        referencias[4].Regra.Should().Be(primeira.Regra);
        referencias.Skip(3).Take(2).Select(item => PonteiroNoDocumento.Converter(item.Campo, configuracao.Origem))
            .Should().Equal("/classificacao/regrasEliminacao/0/regra", "/classificacao/regrasEliminacao/1/regra");
    }

    private static void ConferirCamposDaClassificacao((string Campo, ReferenciaRegra Regra)[] referencias) =>
        referencias.Select(static item => item.Campo).Should().Equal(
            "classificacao.regraCalculo", "classificacao.regraArredondamento", "classificacao.regraOrdemAlocacao",
            "classificacao.regrasEliminacao[1].regra", "classificacao.regrasEliminacao[0].regra",
            "classificacao.criteriosDeDesempate[0].regra");

    [Fact]
    public void De_CriteriosForaDaOrdemDeEntrada_PreservaOcorrencias()
    {
        ConfiguracaoDeclarada configuracao = ConfiguracaoComCriterios();
        (string Campo, ReferenciaRegra Regra)[] referencias = [.. ReferenciasDeRegraDaConfiguracao.De(configuracao)
            .Where(static item => item.Campo.StartsWith("classificacao.criterios", StringComparison.Ordinal))];

        referencias.Select(static item => item.Regra).Should().Equal(configuracao.Avaliacao.CriteriosDesempate[1].Regra, configuracao.Avaliacao.CriteriosDesempate[0].Regra);
        referencias.Select(item => PonteiroNoDocumento.Converter(item.Campo, configuracao.Origem)).Should().Equal(
            "/classificacao/criteriosDeDesempate/0/regra", "/classificacao/criteriosDeDesempate/1/regra");
    }

    private static ConfiguracaoDeclarada ConfiguracaoComCriterios()
    {
        ConfiguracaoDeclarada original = ProcessoConformeFactory.Criar().Declaracao();
        AvaliacaoDeclarada avaliacao = new([], [Criterio(1, 'a'), Criterio(2, 'b')], null);
        return new ConfiguracaoDeclarada(original.Identidade, original.Cronograma, avaliacao, original.Vagas,
            original.Exigencias, original.Coleta, original.DivulgacaoETaxa)
        {
            Origem = new OrigemNoDocumento(new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal)
            {
                ["classificacao.criteriosDeDesempate"] = [1, 0],
            }, []),
        };
    }

    private static CriterioDesempate Criterio(int ordem, char hash) => CriterioDesempate.Criar(
        ordem, ReferenciaRegra.De(CriterioDesempateCodigo.MaiorIdade, "v1", new string(hash, 64)),
        new ParametrosDesempateMaiorIdade()).Value!;

    private static ConfiguracaoDeclarada Configuracao(RegraEliminacao primeira, RegraEliminacao segunda)
    {
        ConfiguracaoDeclarada original = ProcessoConformeFactory.Criar().Declaracao();
        AvaliacaoDeclarada avaliacao = new(original.Avaliacao.Etapas, original.Avaliacao.CriteriosDesempate, Classificacao(primeira, segunda));
        return new ConfiguracaoDeclarada(original.Identidade, original.Cronograma, avaliacao, original.Vagas,
            original.Exigencias, original.Coleta, original.DivulgacaoETaxa)
        {
            Origem = new OrigemNoDocumento(new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal)
            {
                ["classificacao.regrasEliminacao"] = [1, 0],
            }, []),
        };
    }

    internal static ConfiguracaoClassificacao Classificacao(RegraEliminacao primeira, RegraEliminacao segunda) =>
        ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ProcessoConformeFactory.Regra(RegraCalculoCodigo.FormulaMediaPonderada, "c"),
            RegraArredondamento = ProcessoConformeFactory.Regra(RegraArredondamentoCodigo.PrecisaoTruncar, "d"),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = ProcessoConformeFactory.Regra(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "e"),
            RegrasEliminacao = [primeira, segunda],
            EscalaNotaFinal = 100m,
        }).Value!;

    internal static RegraEliminacao Eliminacao(char hash) => RegraEliminacao.Criar(
        ReferenciaRegra.De(RegraEliminacaoCodigo.ElimNotaMinimaEtapa, "v1", new string(hash, 64)),
        new ParametrosElimNotaMinimaEtapa(Guid.CreateVersion7(), 4m)).Value!;
}
