namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.ConferenciaDaConfiguracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class OrdemDasReferenciasDeRegraTests
{
    [Fact]
    public void De_ConfiguracaoNula_RecusaArgumento()
    {
        Action consultar = () => ReferenciasDeRegraDaConfiguracao.De(null!);

        consultar.Should().Throw<ArgumentNullException>().WithParameterName("configuracao");
    }

    [Fact]
    public void De_ConfiguracaoComReferencias_DevolveCamposNaOrdemDosBlocos()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar();
        processo.DefinirAlgoritmoContagemPrazo(ProcessoConformeFactory.Regra("CONTAGEM", "a"));

        string[] campos = [.. ReferenciasDeRegraDaConfiguracao.De(processo.Declaracao()).Select(static item => item.Campo)];

        campos.Should().Equal(
            "identificacao.algoritmoContagemPrazo",
            "distribuicaoVagas[0].regraDistribuicao",
            "classificacao.regraCalculo",
            "classificacao.regraOrdemAlocacao",
            "classificacao.criteriosDeDesempate[0].regra");
    }

    [Fact]
    public void De_OfertasEmOrdemDiferenteDaOrigem_PreservaCadaOcorrenciaNaOrdemDeEntrada()
    {
        ConfiguracaoDeclarada configuracao = ComOfertasEmOutraOrdem();

        (string Campo, ReferenciaRegra Regra)[] ofertas = [.. ReferenciasDeRegraDaConfiguracao.De(configuracao)
            .Where(static item => item.Campo.StartsWith("distribuicaoVagas", StringComparison.Ordinal))];

        ofertas.Select(static item => item.Campo).Should().Equal("distribuicaoVagas[1].regraDistribuicao", "distribuicaoVagas[0].regraDistribuicao");
        ofertas.Select(item => PonteiroNoDocumento.Converter(item.Campo, configuracao.Origem))
            .Should().Equal("/distribuicaoVagas/0/regraDistribuicao", "/distribuicaoVagas/1/regraDistribuicao");
        ofertas.Should().HaveCount(2);
        ofertas[0].Regra.Should().Be(ofertas[1].Regra);
    }

    [Fact]
    public void De_ConfiguracaoSemReferenciasOpcionais_NaoInventaOcorrencias()
    {
        ConfiguracaoDeclarada configuracao = ProcessoConformeFactory.Criar().Declaracao();

        string[] campos = [.. ReferenciasDeRegraDaConfiguracao.De(configuracao).Select(static item => item.Campo)];

        campos.Should().NotContain("identificacao.algoritmoContagemPrazo");
        campos.Should().NotContain("distribuicaoVagas[0].regraAjuste");
        campos.Should().NotContain("ordemDeRemanejamento.regra");
        campos.Should().NotContain("classificacao.regraArredondamento");
        campos.Should().NotContain("fada.regra");
    }

    [Fact]
    public void De_FasesForaDaOrdemDeEntrada_EnumeraAsRegrasNaOrdemDeEntrada()
    {
        ConfiguracaoDeclarada original = ProcessoConformeFactory.Criar().Declaracao();
        FaseCronograma primeira = ProcessoConformeFactory.FaseConforme(ModeloDeFase.RecursoAncoradoEm(ProcessoConformeFactory.ProdutoPreliminarId));
        FaseCronograma segunda = ProcessoConformeFactory.FaseConforme(ModeloDeFase.RecursoAncoradoEm(ProcessoConformeFactory.ProdutoPreliminarId));
        CronogramaDeclarado cronograma = new(null, null, [primeira, segunda]);
        ConfiguracaoDeclarada configuracao = new(original.Identidade, cronograma, original.Avaliacao, original.Vagas,
            original.Exigencias, original.Coleta, original.DivulgacaoETaxa)
        {
            Origem = new OrigemNoDocumento(new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal) { ["fases"] = [1, 0] }, []),
        };

        (string Campo, ReferenciaRegra Regra)[] referencias = [.. ReferenciasDeRegraDaConfiguracao.De(configuracao)
            .Where(static item => item.Campo.StartsWith("fases", StringComparison.Ordinal))];

        ConferirReferenciasDasFases(configuracao, referencias, segunda, primeira);
    }

    [Fact]
    public void De_ProcedimentosForaDaOrdemDeEntrada_PreservaOrdemECampos()
    {
        ConfiguracaoDeclarada configuracao = ComProcedimentosEmOutraOrdem();
        ProcedimentosDeRecurso procedimentos = configuracao.Cronograma.CronogramaFases[0].RegraRecurso!.Procedimentos;

        (string Campo, ReferenciaRegra Regra)[] referencias = [.. ReferenciasDeRegraDaConfiguracao.De(configuracao)
            .Where(static item => item.Campo.StartsWith("fases", StringComparison.Ordinal))];

        referencias.Select(static item => item.Campo).Should().Equal("fases[0].regraRecurso.regraCodigo",
            "fases[0].regraRecurso.procedimentos[1].regra", "fases[0].regraRecurso.procedimentos[0].regra");
        referencias.Skip(1).Select(static item => item.Regra).Should().Equal(procedimentos.Itens[1].Regra, procedimentos.Itens[0].Regra);
        referencias.Select(item => PonteiroNoDocumento.Converter(item.Campo, configuracao.Origem)).Should().Equal(
            "/cronograma/fases/0/regraRecurso/regraCodigo", "/cronograma/fases/0/regraRecurso/procedimentosDeRecurso/0/regra",
            "/cronograma/fases/0/regraRecurso/procedimentosDeRecurso/1/regra");
    }

    private static ConfiguracaoDeclarada ComProcedimentosEmOutraOrdem()
    {
        ConfiguracaoDeclarada original = ProcessoConformeFactory.Criar().Declaracao();
        RegraRecursoFase prazo = ModeloDeFase.RecursoAncoradoEm(ProcessoConformeFactory.ProdutoPreliminarId);
        ProcedimentosDeRecurso procedimentos = Procedimentos();
        RegraRecursoFase recurso = RegraRecursoFase.Criar(prazo.Regra, ParametrosDoRecursoDeTeste.DeclaradosDe(prazo.Parametros), prazo.ProdutoAncoraId, procedimentos).Value!;
        FaseCronograma fase = ProcessoConformeFactory.FaseConforme(recurso);
        return new ConfiguracaoDeclarada(original.Identidade, new CronogramaDeclarado(null, null, [fase]), original.Avaliacao,
            original.Vagas, original.Exigencias, original.Coleta, original.DivulgacaoETaxa)
        {
            Origem = new OrigemNoDocumento(new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal)
            {
                ["fases[0].regraRecurso.procedimentos"] = [1, 0],
            }, []),
        };
    }

    internal static ProcedimentosDeRecurso Procedimentos() => ProcedimentosDeRecurso.De([
        ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.Classificacao, null, ProcessoConformeFactory.Regra("PRIMEIRA", "a")),
        ProcedimentoDeRecursoDeclarado.De(TipoMotivoDeDecisao.IsencaoDaTaxa, null, ProcessoConformeFactory.Regra("SEGUNDA", "b")),
    ]);

    private static void ConferirReferenciasDasFases(ConfiguracaoDeclarada configuracao, (string Campo, ReferenciaRegra Regra)[] referencias,
        FaseCronograma primeira, FaseCronograma segunda)
    {
        referencias.Select(static item => item.Campo)
            .Should().Equal("fases[1].regraRecurso.regraCodigo", "fases[0].regraRecurso.regraCodigo");
        referencias.Select(item => PonteiroNoDocumento.Converter(item.Campo, configuracao.Origem))
            .Should().Equal("/cronograma/fases/0/regraRecurso/regraCodigo", "/cronograma/fases/1/regraRecurso/regraCodigo");
        referencias.Select(static item => item.Regra).Should().Equal(primeira.RegraRecurso!.Regra, segunda.RegraRecurso!.Regra);
    }

    private static ConfiguracaoDeclarada ComOfertasEmOutraOrdem()
    {
        ConfiguracaoDeclarada original = ProcessoConformeFactory.Criar().Declaracao();
        VagasDeclaradas vagas = new([ProcessoConformeFactory.Oferta(), ProcessoConformeFactory.Oferta()], null, false, null, null, null);
        return new ConfiguracaoDeclarada(original.Identidade, original.Cronograma, original.Avaliacao, vagas,
            original.Exigencias, original.Coleta, original.DivulgacaoETaxa)
        {
            Origem = new OrigemNoDocumento(new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal)
            {
                ["distribuicaoVagas"] = [1, 0],
            }, []),
        };
    }
}
