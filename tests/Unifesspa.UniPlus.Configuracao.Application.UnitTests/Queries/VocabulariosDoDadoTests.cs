namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.Cenarios;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;

public sealed class VocabulariosDoDadoTests
{
    [Fact]
    public void Handle_TiposDoDado_DevolveCodigosERotulos()
    {
        IReadOnlyList<CodigoComRotulo<TipoDoDado>> itens = ListarTiposDoDadoQueryHandler.Handle(new ListarTiposDoDadoQuery());
        itens.Select(static item => (item.Codigo, item.Rotulo)).Should().Equal(
            ("SIM_OU_NAO", "Sim ou não"), ("OPCAO", "Opção"), ("NUMERICO", "Número"),
            ("TEXTO", "Texto"), ("DATA", "Data"), ("ENDERECO", "Endereço"));
    }

    [Fact]
    public void Handle_QuantidadesDeValores_DevolveCodigosERotulos()
    {
        IReadOnlyList<CodigoComRotulo<QuantidadeDeValores>> itens = ListarQuantidadesDeValoresQueryHandler.Handle(new ListarQuantidadesDeValoresQuery());
        itens.Select(static item => (item.Codigo, item.Rotulo)).Should().Equal(
            ("VALOR_UNICO", "Um valor"), ("VARIOS_VALORES", "Vários valores"));
    }

    [Theory]
    [InlineData("CATEGORICO")]
    [InlineData("BOOLEANO")]
    public void TentarObter_TipoAntigo_Recusa(string codigo)
    {
        CatalogoDe<TipoDoDado>.TentarObter(codigo, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("ESCALAR")]
    [InlineData("MULTIVALORADO")]
    public void TentarObter_QuantidadeAntiga_Recusa(string codigo)
    {
        CatalogoDe<QuantidadeDeValores>.TentarObter(codigo, out _).Should().BeFalse();
    }

    [Fact]
    public void TentarObter_OrigemAntiga_Recusa()
    {
        CatalogoDe<OrigemFato>.TentarObter("DERIVADO", out _).Should().BeFalse();
    }

    [Fact]
    public void Serializar_Dado_DevolveTipoEQuantidadeComCodigoERotulo()
    {
        FatoCandidatoView dado = DadoDoCandidatoBuilder.Construir();
        JsonNode json = JsonSerializer.SerializeToNode(dado, JsonSerializerOptions.Web)!;
        json["dominio"]!["codigo"]!.ToString().Should().Be("OPCAO");
        json["dominio"]!["rotulo"]!.ToString().Should().Be("Opção");
        json["cardinalidade"]!["codigo"]!.ToString().Should().Be("VARIOS_VALORES");
        json["cardinalidade"]!["rotulo"]!.ToString().Should().Be("Vários valores");
    }
}
