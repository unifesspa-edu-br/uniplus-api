namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

/// <summary>
/// Garante que os campos numéricos obrigatórios dos commands rejeitam a omissão no
/// JSON (via <c>[JsonRequired]</c>), em vez de o System.Text.Json construir o record
/// com <c>0m</c> — um valor que o validator aceitaria e que sobrescreveria
/// silenciosamente o estado da linha numa atualização (#729).
/// </summary>
public sealed class PesoAreaEnemCommandJsonRequiredTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    [Fact(DisplayName = "Atualizar: omitir corteRedacao no JSON é rejeitado (não vira 0 silencioso)")]
    public void Atualizar_SemCorte_LancaJsonException()
    {
        const string json = """
        {
          "id": "0199e3a0-0000-7000-8000-000000000000",
          "pesoRedacao": 1.5, "pesoCienciasNatureza": 1.0, "pesoCienciasHumanas": 1.0,
          "pesoLinguagens": 1.0, "pesoMatematica": 2.0, "baseLegal": "Res. 805/2024 Anexo I"
        }
        """;

        Action act = () => JsonSerializer.Deserialize<AtualizarPesoAreaEnemCommand>(json, Options);

        act.Should().Throw<JsonException>();
    }

    [Fact(DisplayName = "Atualizar: omitir um peso no JSON é rejeitado")]
    public void Atualizar_SemPeso_LancaJsonException()
    {
        const string json = """
        {
          "id": "0199e3a0-0000-7000-8000-000000000000",
          "pesoRedacao": 1.5, "pesoCienciasNatureza": 1.0, "pesoCienciasHumanas": 1.0,
          "pesoLinguagens": 1.0, "corteRedacao": 450.0, "baseLegal": "Res. 805/2024 Anexo I"
        }
        """;

        Action act = () => JsonSerializer.Deserialize<AtualizarPesoAreaEnemCommand>(json, Options);

        act.Should().Throw<JsonException>();
    }

    [Fact(DisplayName = "Atualizar: JSON completo desserializa")]
    public void Atualizar_Completo_Desserializa()
    {
        const string json = """
        {
          "id": "0199e3a0-0000-7000-8000-000000000000",
          "pesoRedacao": 1.5, "pesoCienciasNatureza": 1.0, "pesoCienciasHumanas": 1.0,
          "pesoLinguagens": 1.0, "pesoMatematica": 2.0, "corteRedacao": 450.0,
          "baseLegal": "Res. 805/2024 Anexo I"
        }
        """;

        AtualizarPesoAreaEnemCommand? cmd = JsonSerializer.Deserialize<AtualizarPesoAreaEnemCommand>(json, Options);

        cmd.Should().NotBeNull();
        cmd!.CorteRedacao.Should().Be(450.0m);
    }

    [Fact(DisplayName = "Criar: omitir corteRedacao desserializa com null (mantém opcional/default 400)")]
    public void Criar_SemCorte_DesserializaComNull()
    {
        const string json = """
        {
          "resolucao": "Res. 805/2024", "grupoCurso": "Tecnológica",
          "pesoRedacao": 1.5, "pesoCienciasNatureza": 1.0, "pesoCienciasHumanas": 1.0,
          "pesoLinguagens": 1.0, "pesoMatematica": 2.0, "baseLegal": "Res. 805/2024 Anexo I"
        }
        """;

        CriarPesoAreaEnemCommand? cmd = JsonSerializer.Deserialize<CriarPesoAreaEnemCommand>(json, Options);

        cmd.Should().NotBeNull();
        cmd!.CorteRedacao.Should().BeNull();
    }

    [Fact(DisplayName = "Criar: omitir um peso no JSON é rejeitado")]
    public void Criar_SemPeso_LancaJsonException()
    {
        const string json = """
        {
          "resolucao": "Res. 805/2024", "grupoCurso": "Tecnológica",
          "pesoCienciasNatureza": 1.0, "pesoCienciasHumanas": 1.0,
          "pesoLinguagens": 1.0, "pesoMatematica": 2.0, "baseLegal": "Res. 805/2024 Anexo I"
        }
        """;

        Action act = () => JsonSerializer.Deserialize<CriarPesoAreaEnemCommand>(json, Options);

        act.Should().Throw<JsonException>();
    }
}
