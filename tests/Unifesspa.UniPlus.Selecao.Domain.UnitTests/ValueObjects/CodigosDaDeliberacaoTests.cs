namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class CodigosDaDeliberacaoTests
{
    private static DeclaracaoDaDeliberacao Valida() =>
        new("COLEGIADA_COM_VOTACAO", 3, 0, new(2, "MAIORIA_SIMPLES", "VOTO_DA_PRESIDENCIA", "PRESIDENCIA_NO_QUORUM", null));

    [Fact]
    public void Criar_ModoDesconhecidoEIntegrantesInvalidos_AcumulaAsRecusas()
    {
        DeclaracaoDaDeliberacao declarada = Valida() with { Modo = "outro", Integrantes = 0 };
        Result<RegraDeDeliberacao> resultado = RegraDeDeliberacao.Criar(declarada);

        resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ("modo", "Banca.ModoDeDeliberacaoInvalido"), ("integrantes", "Banca.IntegrantesInvalidos"));
    }

    [Fact]
    public void Criar_ModoDesconhecidoEComposicaoInvalida_AcumulaAsRecusasEmOrdem()
    {
        DeclaracaoDaDeliberacao declarada = Valida() with { Modo = "outro", Integrantes = 0, Suplentes = -1 };
        Result<RegraDeDeliberacao> resultado = RegraDeDeliberacao.Criar(declarada);

        resultado.Errors.Select(static erro => (erro.Field, erro.Error.Code)).Should().Equal(
            ("modo", "Banca.ModoDeDeliberacaoInvalido"), ("integrantes", "Banca.IntegrantesInvalidos"),
            ("suplentes", "Banca.SuplentesInvalidos"));
    }

    [Fact]
    public void Criar_CodigosDesconhecidos_AcumulaSemRepetirOsValores()
    {
        DeclaracaoDaDeliberacao declarada = Valida() with { Votacao = Valida().Votacao with { Criterio = "MAIORIASIMPLES", Encerramento = "outro" } };
        Result<RegraDeDeliberacao> resultado = RegraDeDeliberacao.Criar(declarada);

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal("Banca.CriterioObrigatorio", "Banca.EncerramentoObrigatorio");
        resultado.Errors.Should().OnlyContain(static e => !e.Error.Message.Contains("MAIORIASIMPLES") && !e.Error.Message.Contains("outro"));
    }

    [Fact]
    public void Criar_AtoIndividualComCriterioDesconhecido_RecusaOpcaoSemDizerQueFalta()
    {
        DeclaracaoDaDeliberacao declarada = Valida() with { Modo = "ATO_INDIVIDUAL", Votacao = Valida().Votacao with { Criterio = "outro" } };
        Result<RegraDeDeliberacao> resultado = RegraDeDeliberacao.Criar(declarada);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be("Banca.CriterioObrigatorio");
    }

    [Theory]
    [InlineData("modo")]
    [InlineData("desempate")]
    [InlineData("semUnanimidade")]
    public void Criar_CodigoDesconhecido_RecusaOCampo(string campo)
    {
        DeclaracaoDaDeliberacao declarada = campo switch
        {
            "modo" => Valida() with { Modo = "outro" },
            "desempate" => Valida() with { Votacao = Valida().Votacao with { Desempate = "outro" } },
            _ => Valida() with { Votacao = Valida().Votacao with { Criterio = "UNANIMIDADE", Desempate = null, SemUnanimidade = "outro" } },
        };
        RegraDeDeliberacao.Criar(declarada).Errors.Should().ContainSingle(e => e.Field == campo);
    }
}
