namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DeclaracaoDoNoDeExigenciaTests
{
    [Fact]
    public void Interpretar_TipoDesconhecido_RecusaSemRepetirValor()
    {
        DeclaracaoDoNoDeExigencia declaracao = DeclaracaoDoNoDeExigencia.Interpretar("XOR", false, 0, null);

        declaracao.Tipo.Should().Be(TipoNo.Nenhum);
        declaracao.Recusas.Should().ContainSingle().Which.Error.Message.Should().NotContain("XOR");
        declaracao.Recusas[0].Error.Code.Should().Be("NoExigencia.TipoInvalido");
    }

    [Fact]
    public void Interpretar_FolhaSemDocumento_RecusaNoCampo()
    {
        DeclaracaoDoNoDeExigencia declaracao = DeclaracaoDoNoDeExigencia.Interpretar("FOLHA", false, 0, null);

        declaracao.Recusas.Should().ContainSingle().Which.Field.Should().Be("documento");
        declaracao.Recusas[0].Error.Code.Should().Be("NoExigencia.DocumentoObrigatorioEmFolha");
    }
    [Theory]
    [InlineData("E")]
    [InlineData("OU")]
    public void Interpretar_GrupoComDocumento_Recusa(string tipo)
    {
        DeclaracaoDoNoDeExigencia declaracao = DeclaracaoDoNoDeExigencia.Interpretar(tipo, true, 1, null);

        declaracao.Recusas.Should().ContainSingle().Which.Error.Code.Should().Be("NoExigencia.DocumentoProibidoEmGrupo");
        declaracao.Recusas[0].Field.Should().Be("documento");
    }

    [Fact]
    public void Interpretar_FolhaComFilhos_Recusa()
    {
        DeclaracaoDoNoDeExigencia declaracao = DeclaracaoDoNoDeExigencia.Interpretar("FOLHA", true, 1, null);

        declaracao.Recusas.Should().ContainSingle().Which.Error.Code.Should().Be("NoExigencia.FilhosProibidosEmFolha");
        declaracao.Recusas[0].Field.Should().Be("filhos");
    }
    [Theory]
    [InlineData("OU", 0, null, "filhos", "NoExigencia.GrupoVazio")]
    [InlineData("E", 1, 1, "quantidadeMinima", "NoExigencia.QuantidadeMinimaProibidaEmGrupoE")]
    [InlineData("OU", 1, 0, "quantidadeMinima", "NoExigencia.QuantidadeMinimaForaDosLimites")]
    [InlineData("FOLHA", 0, 0, "quantidadeMinima", "NoExigencia.QuantidadeMinimaDeFolhaInvalida")]
    public void Interpretar_QuantidadeOuGrupoInvalido_Recusa(string tipo, int filhos, int? quantidade, string campo, string codigo)
    {
        DeclaracaoDoNoDeExigencia declaracao = DeclaracaoDoNoDeExigencia.Interpretar(tipo, tipo == "FOLHA", filhos, quantidade);

        declaracao.Recusas.Should().ContainSingle().Which.Should().Match<Unifesspa.UniPlus.Kernel.Results.FieldError>(
            recusa => recusa.Field == campo && recusa.Error.Code == codigo);
    }

    [Theory]
    [InlineData("E", "ELIMINA", false, "consequencia", "NoExigencia.ConsequenciaProibidaEmGrupoE")]
    [InlineData("E", null, true, "basesLegais", "NoExigencia.BaseLegalProibidaEmGrupoE")]
    [InlineData("OU", "INVALIDA", false, "consequencia", "NoExigencia.ConsequenciaInvalida")]
    [InlineData("OU", null, true, "basesLegais", "NoExigencia.BaseLegalSemConsequencia")]
    public void Interpretar_ConsequenciaOuBaseInvalida_Recusa(string tipo, string? consequencia, bool temBase, string campo, string codigo)
    {
        DeclaracaoDoNoDeExigencia declaracao = DeclaracaoDoNoDeExigencia.Interpretar(tipo, false, 1, null, consequencia, temBase);

        declaracao.Recusas.Should().ContainSingle().Which.Should().Match<Unifesspa.UniPlus.Kernel.Results.FieldError>(
            recusa => recusa.Field == campo && recusa.Error.Code == codigo);
    }

    [Fact]
    public void Interpretar_GrupoComDadosNormalizaveis_GuardaOsDadosNormalizados()
    {
        DeclaracaoDoNoDeExigencia declaracao = DeclaracaoDoNoDeExigencia.Interpretar("OU", false, 1, null, " ELIMINA ");

        declaracao.Tipo.Should().Be(TipoNo.GrupoOu);
        declaracao.QuantidadeDeFilhos.Should().Be(1);
        declaracao.QuantidadeMinima.Should().Be(1);
        declaracao.Consequencia.Should().Be("ELIMINA");
        declaracao.Recusas.Should().BeEmpty();
    }

    [Fact]
    public void Interpretar_DistincaoComProblemasIndependentes_Acumula()
    {
        DistincaoDeclarada distincao = new(ChaveDistincao.CompetenciaMensal, null, ["OUTRA"]);

        DeclaracaoDoNoDeExigencia declaracao = DeclaracaoDoNoDeExigencia.Interpretar("FOLHA", true, 0, 1, distincao: distincao);

        declaracao.Distincao.Should().Be(distincao);
        declaracao.Recusas.Select(static r => (r.Field, r.Error.Code)).Should().Equal(
            ("dataReferencia", "NoExigencia.DataReferenciaObrigatoriaParaChaveCalendario"),
            ("ocorrenciasEsperadas", "NoExigencia.OcorrenciasEsperadasIndevidasParaChave"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("DESCONHECIDO")]
    public void Interpretar_CriterioDesconhecido_RecusaSemRepetirValor(string codigo)
    {
        DistincaoDeclarada distincao = DistincaoDeclarada.Interpretar(codigo, null, null);

        DeclaracaoDoNoDeExigencia declaracao = DeclaracaoDoNoDeExigencia.Interpretar("FOLHA", true, 0, 1, distincao: distincao);

        declaracao.Recusas.Should().ContainSingle().Which.Error.Code.Should().Be("NoExigencia.ChaveDistincaoInvalida");
        declaracao.Recusas[0].Field.Should().Be("chaveDistincao");
    }

}
