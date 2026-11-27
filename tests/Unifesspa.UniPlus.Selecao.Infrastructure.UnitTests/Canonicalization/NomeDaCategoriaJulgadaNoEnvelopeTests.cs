namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class NomeDaCategoriaJulgadaNoEnvelopeTests
{
    [Fact]
    public void SerializarFase_NomeDecomposto_GravaEmNfcERestaura()
    {
        JsonObject fase = SerializacaoDoCronograma.SerializarFase(FaseComCategoria());
        JsonObject banca = JsonNode.Parse(fase["bancasRequeridas"]![0]!.ToJsonString())!.AsObject();
        LeitorEnvelope leitor = new();
        List<CategoriaJulgada> categorias = LeituraDosPrazosDeRecurso.LerCandidatosAtendidos(leitor, banca, "banca");

        leitor.Falhou.Should().BeFalse();
        banca["candidatosAtendidos"]![0]!["nome"]!.GetValue<string>().Should().Be("Seleção");
        categorias.Single().Nome.Should().Be("Seleção");
    }

    private static FaseCronograma FaseComCategoria()
    {
        TipoDeBancaCopiado tipo = new(Guid.NewGuid(), TipoDeBanca.AnaliseDocumental, "Banca", PapelDaBanca.AvaliaOMerito, false, false, []);
        CategoriaJulgada categoria = CategoriaJulgada.Criar(Guid.NewGuid(), "RENDA", "Selec\u0327a\u0303o");
        BancaRequerida banca = BancaRequerida.Criar(ChaveLocal.Criar("banca").Value, tipo, [categoria]);
        OrigemCanonicaDaFase origem = OrigensCanonicasDeTeste.Da(Guid.NewGuid(), "SELECAO", OrigemDataFase.NoEdital, false, false, false, false) with { Nome = "Seleção" };
        return FaseCronograma.Reidratar(ChaveLocal.Criar("fase_selecao").Value, Guid.NewGuid(), origem,
            new DecisoesDeCronogramaDaFase(1, null, null, null), new PublicacoesDaFase([], false, null), [banca]);
    }

    [Fact]
    public void LerCandidatosAtendidos_NomeInformado_RestauraONome()
    {
        LeitorEnvelope leitor = new();
        List<CategoriaJulgada> categorias = LeituraDosPrazosDeRecurso.LerCandidatosAtendidos(leitor, Banca("Renda"), "banca");

        leitor.Falhou.Should().BeFalse();
        categorias.Should().ContainSingle().Which.Nome.Should().Be("Renda");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void LerCandidatosAtendidos_NomeAusenteOuVazio_Recusa(string? nome)
    {
        JsonObject banca = Banca(nome);
        if (nome is null)
        {
            banca["candidatosAtendidos"]![0]!.AsObject().Remove("nome");
        }
        LeitorEnvelope leitor = new();
        LeituraDosPrazosDeRecurso.LerCandidatosAtendidos(leitor, banca, "banca");
        leitor.Falhou.Should().BeTrue();
    }

    [Fact]
    public void LerCandidatosAtendidos_NomeAcimaDoLimite_Recusa()
    {
        LeitorEnvelope leitor = new();
        LeituraDosPrazosDeRecurso.LerCandidatosAtendidos(leitor, Banca(new string('a', LimitesDoEnvelope.CategoriaDocumentoNome + 1)), "banca");
        leitor.Falhou.Should().BeTrue();
    }

    private static JsonObject Banca(string? nome) => new()
    {
        ["candidatosAtendidos"] = new JsonArray(new JsonObject
        {
            ["categoriaDocumentoOrigemId"] = Guid.NewGuid().ToString(),
            ["codigo"] = "RENDA",
            ["nome"] = nome,
        }),
    };
}
