namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura do hash content-addressable do <c>rol_de_regras</c>
/// (<see cref="HashCanonicalComputer.ComputeRegraCatalogo"/>): determinismo,
/// independência da ordem das chaves do jsonb e sensibilidade a mudança de
/// definição — as propriedades que sustentam a reprodutibilidade do freeze
/// (RN08).
/// </summary>
public sealed class HashCanonicalComputerRegraCatalogoTests
{
    private static JsonElement Json(string raw)
    {
        using JsonDocument document = JsonDocument.Parse(raw);
        return document.RootElement.Clone();
    }

    [Fact(DisplayName = "Hash é determinístico para a mesma definição")]
    public void Hash_MesmaDefinicao_Deterministico()
    {
        string h1 = HashCanonicalComputer.ComputeRegraCatalogo(
            "BONUS-MULTIPLICATIVO", "v1", TipoRegra.RegraBonus,
            Json("""{"fator":"numeric","teto":"numeric|null"}"""),
            Json("""["nota_final × fator, após os pesos"]"""),
            "RN05");

        string h2 = HashCanonicalComputer.ComputeRegraCatalogo(
            "BONUS-MULTIPLICATIVO", "v1", TipoRegra.RegraBonus,
            Json("""{"fator":"numeric","teto":"numeric|null"}"""),
            Json("""["nota_final × fator, após os pesos"]"""),
            "RN05");

        h1.Should().Be(h2);
        HashCanonicalComputer.IsValidHashShape(h1).Should().BeTrue();
    }

    [Fact(DisplayName = "Hash independe da ordem das chaves do esquema_args (canonicalização)")]
    public void Hash_OrdemDasChaves_NaoAltera()
    {
        string ordemA = HashCanonicalComputer.ComputeRegraCatalogo(
            "R", "v1", TipoRegra.RegraBonus,
            Json("""{"fator":"numeric","teto":"numeric|null"}"""),
            Json("[]"),
            "base");

        string ordemB = HashCanonicalComputer.ComputeRegraCatalogo(
            "R", "v1", TipoRegra.RegraBonus,
            Json("""{"teto":"numeric|null","fator":"numeric"}"""),
            Json("[]"),
            "base");

        ordemB.Should().Be(ordemA);
    }

    [Fact(DisplayName = "Hash muda quando qualquer campo definicional muda")]
    public void Hash_MudancaDeDefinicao_MudaHash()
    {
        string baseHash = HashCanonicalComputer.ComputeRegraCatalogo(
            "R", "v1", TipoRegra.RegraBonus, Json("""{"fator":"numeric"}"""), Json("[]"), "base-a");

        HashCanonicalComputer.ComputeRegraCatalogo(
            "R", "v2", TipoRegra.RegraBonus, Json("""{"fator":"numeric"}"""), Json("[]"), "base-a")
            .Should().NotBe(baseHash, "mudar a versão muda o hash");

        HashCanonicalComputer.ComputeRegraCatalogo(
            "R", "v1", TipoRegra.RegraBonus, Json("""{"fator":"numeric"}"""), Json("[]"), "base-b")
            .Should().NotBe(baseHash, "mudar a base legal muda o hash");

        HashCanonicalComputer.ComputeRegraCatalogo(
            "R", "v1", TipoRegra.RegraArredondamento, Json("""{"fator":"numeric"}"""), Json("[]"), "base-a")
            .Should().NotBe(baseHash, "mudar o tipo muda o hash");
    }
}
