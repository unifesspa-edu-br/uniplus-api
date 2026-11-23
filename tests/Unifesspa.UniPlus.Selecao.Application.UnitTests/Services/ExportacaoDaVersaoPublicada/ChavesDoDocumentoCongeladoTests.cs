namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.ExportacaoDaVersaoPublicada;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;

public sealed class ChavesDoDocumentoCongeladoTests
{
    [Fact]
    public void Resolver_IdentificadoresDoCongelado_DevolveAsChavesCorrespondentes()
    {
        ChavesDoDocumentoCongelado chaves = CriarChaves();
        chaves.Fase("f1").Should().Be("fase");
        chaves.FasePeloCodigo("INSCRICAO").Should().Be("fase");
        chaves.Etapa("e1").Should().Be("etapa");
        chaves.ItemDeAvaliacao("i1").Should().Be("item");
        chaves.Exigencia("d1").Should().Be("documento");
    }

    [Fact]
    public void Fase_IdentificadorNaoResolvido_LancaExcecao()
    {
        ChavesDoDocumentoCongelado chaves = new(new JsonObject());
        Action resolver = () => chaves.Fase("ausente");
        resolver.Should().Throw<InvalidOperationException>().WithMessage("*ausente*fase*");
    }
    [Theory]
    [InlineData("[]")]
    [InlineData("[{\"tipoFaseOrigemId\":\"t1\",\"chave\":\"a\"},{\"tipoFaseOrigemId\":\"t1\",\"chave\":\"b\"}]")]
    public void FasePeloTipo_QuantidadeDeFasesDiferenteDeUm_LancaExcecao(string fases)
    {
        JsonObject congelado = new() { ["cronogramaFases"] = new JsonObject { ["fases"] = JsonNode.Parse(fases) } };
        ChavesDoDocumentoCongelado chaves = new(congelado);
        Action resolver = () => chaves.FasePeloTipo("t1");
        resolver.Should().Throw<InvalidOperationException>();
    }

    private static ChavesDoDocumentoCongelado CriarChaves()
    {
        // As chaves serão acrescentadas pelos serializadores nas etapas 5a, 5b e 5d.
        JsonObject congelado = JsonNode.Parse("""
            {"cronogramaFases":{"fases":[{"id":"f1","codigo":"INSCRICAO","chave":"fase"}]},
             "etapas":[{"id":"e1","chave":"etapa","itensDeAvaliacao":[{"id":"i1","chave":"item"}]}],
             "documentosExigidos":{"exigencias":[{"exigenciaId":"d1","chave":"documento"}]}}
            """)!.AsObject();
        return new(congelado);
    }

}
