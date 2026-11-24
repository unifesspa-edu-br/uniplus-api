namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class HashCanonicalComputerTests
{
    [Fact]
    public void Compute_MesmoConteudo_ProduzMesmoHash()
    {
        string hash = HashCanonicalComputer.Compute(Criar());
        hash.Should().Be(HashCanonicalComputer.Compute(Criar()));
        hash.Should().HaveLength(64).And.MatchRegex("^[0-9a-f]{64}$");
        HashCanonicalComputer.IsValidHashShape(hash).Should().BeTrue();
    }

    [Theory]
    [InlineData("baseLegal")]
    [InlineData("categoria")]
    [InlineData("regraCodigo")]
    [InlineData("tipoProcessoCodigo")]
    [InlineData("predicado")]
    [InlineData("portariaInternaCodigo")]
    [InlineData("vigenciaInicio")]
    [InlineData("vigenciaFim")]
    public void Compute_MudancaEmCampoSemantico_MudaHash(string campo)
    {
        HashCanonicalComputer.Compute(Criar(campo)).Should().NotBe(HashCanonicalComputer.Compute(Criar()));
    }

    [Fact]
    public void Compute_DefinicaoConhecida_PreservaHash()
    {
        string esperado = HashCanonicalComputer.ComputeSha256Hex(HashCanonicalComputer.ComputeSnapshotBytes(Definicao()));
        HashCanonicalComputer.Compute(Criar()).Should().Be(esperado);
    }

    private static JsonObject Definicao() => new()
    {
        ["baseLegal"] = "Lei 12.711/2012 art.1º",
        ["categoria"] = "ETAPA",
        ["portariaInternaCodigo"] = "Portaria 2026/14",
        ["predicado"] = JsonSerializer.SerializeToNode<PredicadoObrigatoriedade>(
            new EtapaObrigatoria("ProvaObjetiva"), HashCanonicalComputer.CanonicalOptions),
        ["regraCodigo"] = "ETAPA_OBRIGATORIA",
        ["tipoProcessoCodigo"] = "*",
        ["vigenciaFim"] = "2027-01-01",
        ["vigenciaInicio"] = "2026-01-01",
    };

    [Fact]
    public void Compute_PredicadosDistintos_ProduzHashesDistintos()
    {
        string etapa = HashCanonicalComputer.Compute(Criar());
        string redacao = HashCanonicalComputer.Compute(Criar("predicado"));
        etapa.Should().NotBe(redacao);
    }

    [Fact]
    public void Compute_PortariaNulaOuEmBranco_ProduzMesmoHash()
    {
        HashCanonicalComputer.Compute(Criar("portariaNula"))
            .Should().Be(HashCanonicalComputer.Compute(Criar("portariaVazia")));
    }

    [Fact]
    public void Compute_VariantesDoPredicado_ProduzHashesDistintos()
    {
        string modalidades = HashCanonicalComputer.Compute(Criar("modalidades"));
        string dupla = HashCanonicalComputer.Compute(Criar("dupla"));
        modalidades.Should().NotBe(dupla).And.NotBe(HashCanonicalComputer.Compute(Criar()));
        dupla.Should().NotBe(HashCanonicalComputer.Compute(Criar()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("gaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("G000000000000000000000000000000000000000000000000000000000000000")]
    public void IsValidHashShape_ValorInvalido_Recusa(string? valor) =>
        HashCanonicalComputer.IsValidHashShape(valor).Should().BeFalse();

    [Fact]
    public void IsValidHashShape_ComprimentoOuMaiusculas_Recusa()
    {
        HashCanonicalComputer.IsValidHashShape(new string('a', 63)).Should().BeFalse();
        HashCanonicalComputer.IsValidHashShape(new string('a', 65)).Should().BeFalse();
        HashCanonicalComputer.IsValidHashShape(new string('A', 64)).Should().BeFalse();
    }

    private static ObrigatoriedadeLegal Criar(string? campo = null) => ObrigatoriedadeLegal.Criar(
        campo == "tipoProcessoCodigo" ? "PSIQ" : "*",
        campo == "categoria" ? CategoriaObrigatoriedade.Modalidade : CategoriaObrigatoriedade.Etapa,
        campo == "regraCodigo" ? "ETAPA_OBRIGATORIA_V2" : "ETAPA_OBRIGATORIA",
        Predicado(campo),
        Texto(campo),
        campo == "vigenciaInicio" ? new DateOnly(2025, 6, 1) : new DateOnly(2026, 1, 1),
        campo == "vigenciaFim" ? null : new DateOnly(2027, 1, 1)).Value!;

    private static PredicadoObrigatoriedade Predicado(string? campo) => campo switch
    {
        "modalidades" => new ModalidadesMinimas(["AC", "LbPpi"]),
        "dupla" => new ConcorrenciaDuplaObrigatoria(),
        "predicado" => new EtapaObrigatoria("Redacao"),
        _ => new EtapaObrigatoria("ProvaObjetiva"),
    };

    private static TextoNormativo Texto(string? campo) => new(
        "Exige etapa de avaliação.",
        campo == "baseLegal" ? "Lei 14.723/2023 art.2º" : "Lei 12.711/2012 art.1º",
        null,
        campo switch
        {
            "portariaInternaCodigo" => "Portaria 2026/99",
            "portariaNula" => null,
            "portariaVazia" => "",
            _ => "Portaria 2026/14",
        });
}
