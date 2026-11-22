namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DeclaracaoDaRegraDeComprovacaoTests
{
    [Fact]
    public void Interpretar_GrupoVazioEQuantidadeZero_AcumulaRecusas()
    {
        DeclaracaoDaRegraDeComprovacao declaracao = DeclaracaoDaRegraDeComprovacao.Interpretar("QUANTIDADE_MINIMA_DE_ITENS", false, 0, 0);

        declaracao.Recusas.Select(static erro => erro.Field).Should().Equal("itens", "quantidadeMinima");
    }

    [Fact]
    public void Interpretar_TipoDesconhecido_RecusaSemRepetirValor()
    {
        DeclaracaoDaRegraDeComprovacao declaracao = DeclaracaoDaRegraDeComprovacao.Interpretar("XOR", false, 0, null);

        declaracao.Forma.Should().BeNull();
        declaracao.Recusas.Should().ContainSingle().Which.Error.Message.Should().NotContain("XOR");
        declaracao.Recusas[0].Error.Code.Should().Be("RegraDeComprovacao.FormaInvalida");
    }

    [Fact]
    public void Interpretar_RegraSemDocumento_RecusaNoCampo()
    {
        DeclaracaoDaRegraDeComprovacao declaracao = DeclaracaoDaRegraDeComprovacao.Interpretar("DOCUMENTO", false, 0, null);

        declaracao.Recusas.Should().ContainSingle().Which.Field.Should().Be("documento");
        declaracao.Recusas[0].Error.Code.Should().Be("RegraDeComprovacao.DocumentoObrigatorio");
    }
    [Theory]
    [InlineData("TODOS_OS_ITENS")]
    [InlineData("QUANTIDADE_MINIMA_DE_ITENS")]
    public void Interpretar_GrupoComDocumento_Recusa(string forma)
    {
        DeclaracaoDaRegraDeComprovacao declaracao = DeclaracaoDaRegraDeComprovacao.Interpretar(forma, true, 1, null);

        declaracao.Recusas.Should().ContainSingle().Which.Error.Code.Should().Be("RegraDeComprovacao.DocumentoProibidoEmGrupo");
        declaracao.Recusas[0].Field.Should().Be("documento");
    }

    [Fact]
    public void Interpretar_RegraDeDocumentoComItens_Recusa()
    {
        DeclaracaoDaRegraDeComprovacao declaracao = DeclaracaoDaRegraDeComprovacao.Interpretar("DOCUMENTO", true, 1, null);

        declaracao.Recusas.Should().ContainSingle().Which.Error.Code.Should().Be("RegraDeComprovacao.ItensProibidosEmRegraDeDocumento");
        declaracao.Recusas[0].Field.Should().Be("itens");
    }
    [Theory]
    [InlineData("QUANTIDADE_MINIMA_DE_ITENS", 0, null, "itens", "RegraDeComprovacao.GrupoSemItens")]
    [InlineData("TODOS_OS_ITENS", 1, 1, "quantidadeMinima", "RegraDeComprovacao.QuantidadeMinimaProibidaQuandoExigeTodos")]
    [InlineData("QUANTIDADE_MINIMA_DE_ITENS", 1, 0, "quantidadeMinima", "RegraDeComprovacao.QuantidadeMinimaDeItensForaDosLimites")]
    [InlineData("DOCUMENTO", 0, 0, "quantidadeMinima", "RegraDeComprovacao.QuantidadeMinimaDeApresentacoesInvalida")]
    public void Interpretar_QuantidadeOuGrupoInvalido_Recusa(string forma, int quantidadeDeItens, int? quantidade, string campo, string codigo)
    {
        DeclaracaoDaRegraDeComprovacao declaracao = DeclaracaoDaRegraDeComprovacao.Interpretar(forma, forma == "DOCUMENTO", quantidadeDeItens, quantidade);

        declaracao.Recusas.Should().ContainSingle().Which.Should().Match<Unifesspa.UniPlus.Kernel.Results.FieldError>(
            recusa => recusa.Field == campo && recusa.Error.Code == codigo);
    }

    [Theory]
    [InlineData("TODOS_OS_ITENS", "ELIMINA", false, "consequencia", "RegraDeComprovacao.ConsequenciaProibidaQuandoExigeTodos")]
    [InlineData("TODOS_OS_ITENS", null, true, "basesLegais", "RegraDeComprovacao.BaseLegalProibidaQuandoExigeTodos")]
    [InlineData("QUANTIDADE_MINIMA_DE_ITENS", "INVALIDA", false, "consequencia", "RegraDeComprovacao.ConsequenciaInvalida")]
    [InlineData("QUANTIDADE_MINIMA_DE_ITENS", null, true, "basesLegais", "RegraDeComprovacao.BaseLegalSemConsequencia")]
    public void Interpretar_ConsequenciaOuBaseInvalida_Recusa(string forma, string? consequencia, bool temBaseLegal, string campo, string codigo)
    {
        DeclaracaoDaRegraDeComprovacao declaracao = DeclaracaoDaRegraDeComprovacao.Interpretar(forma, false, 1, null, consequencia, temBaseLegal);

        declaracao.Recusas.Should().ContainSingle().Which.Should().Match<Unifesspa.UniPlus.Kernel.Results.FieldError>(
            recusa => recusa.Field == campo && recusa.Error.Code == codigo);
    }

    [Fact]
    public void Interpretar_GrupoComDadosNormalizaveis_GuardaOsDadosNormalizados()
    {
        DeclaracaoDaRegraDeComprovacao declaracao = DeclaracaoDaRegraDeComprovacao.Interpretar("QUANTIDADE_MINIMA_DE_ITENS", false, 1, null, " ELIMINA ");

        declaracao.Forma.Should().Be(FormaDeComprovacao.QuantidadeMinimaDeItens);
        declaracao.QuantidadeDeItens.Should().Be(1);
        declaracao.QuantidadeMinima.Should().Be(1);
        declaracao.Consequencia.Should().Be("ELIMINA");
        declaracao.Recusas.Should().BeEmpty();
    }

    [Fact]
    public void Interpretar_DistincaoComProblemasIndependentes_Acumula()
    {
        DistincaoDeclarada distincao = new(CriterioDeDistincaoDasApresentacoes.CompetenciaMensal, null, ["OUTRA"]);

        DeclaracaoDaRegraDeComprovacao declaracao = DeclaracaoDaRegraDeComprovacao.Interpretar("DOCUMENTO", true, 0, 1, distincao: distincao);

        declaracao.Distincao.Should().Be(distincao);
        declaracao.Recusas.Select(static r => (r.Field, r.Error.Code)).Should().Equal(
            ("dataReferencia", "RegraDeComprovacao.DataReferenciaObrigatoriaParaCalendario"),
            ("ocorrenciasEsperadas", "RegraDeComprovacao.OcorrenciasEsperadasIndevidasParaCriterio"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("DESCONHECIDO")]
    public void Interpretar_CriterioDesconhecido_RecusaSemRepetirValor(string codigo)
    {
        DistincaoDeclarada distincao = DistincaoDeclarada.Interpretar(codigo, null, null);

        DeclaracaoDaRegraDeComprovacao declaracao = DeclaracaoDaRegraDeComprovacao.Interpretar("DOCUMENTO", true, 0, 1, distincao: distincao);

        declaracao.Recusas.Should().ContainSingle().Which.Error.Code.Should().Be("RegraDeComprovacao.CriterioDeDistincaoInvalido");
        declaracao.Recusas[0].Field.Should().Be("criterioDeDistincao");
    }

}
