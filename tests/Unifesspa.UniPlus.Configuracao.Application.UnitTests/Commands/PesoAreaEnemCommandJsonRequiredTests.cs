namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.PesosAreaEnem;

/// <summary>
/// Garante que o peso de cada área rejeita a omissão no JSON (via <c>[JsonRequired]</c>),
/// em vez de o System.Text.Json construir o item com <c>0m</c> — um peso válido que
/// sobrescreveria silenciosamente o valor configurado numa atualização. O corte, ao
/// contrário, é opcional: ausente é "sem corte".
/// </summary>
public sealed class PesoAreaEnemCommandJsonRequiredTests
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    [Fact(DisplayName = "Atualizar: omitir o peso de uma área no JSON é rejeitado (não vira 0 silencioso)")]
    public void Atualizar_SemPeso_LancaJsonException()
    {
        const string json = """
        {
          "id": "0199e3a0-0000-7000-8000-000000000000",
          "areas": [
            { "codigo": "REDACAO", "peso": 2.0, "corte": 400 },
            { "codigo": "CIENCIAS_DA_NATUREZA", "peso": 1.5 },
            { "codigo": "CIENCIAS_HUMANAS", "peso": 2.5 },
            { "codigo": "LINGUAGENS", "peso": 2.5 },
            { "codigo": "MATEMATICA" }
          ],
          "baseLegal": "Res. 805/2024 Anexo I"
        }
        """;

        Action act = () => JsonSerializer.Deserialize<AtualizarPesoAreaEnemCommand>(json, Options);

        act.Should().Throw<JsonException>();
    }

    [Fact(DisplayName = "Atualizar: JSON completo desserializa, e o corte ausente fica nulo")]
    public void Atualizar_Completo_Desserializa()
    {
        const string json = """
        {
          "id": "0199e3a0-0000-7000-8000-000000000000",
          "areas": [
            { "codigo": "REDACAO", "peso": 2.0, "corte": 450 },
            { "codigo": "CIENCIAS_DA_NATUREZA", "peso": 1.5 },
            { "codigo": "CIENCIAS_HUMANAS", "peso": 2.5 },
            { "codigo": "LINGUAGENS", "peso": 2.5 },
            { "codigo": "MATEMATICA", "peso": 1.5 }
          ],
          "baseLegal": "Res. 805/2024 Anexo I"
        }
        """;

        AtualizarPesoAreaEnemCommand? cmd = JsonSerializer.Deserialize<AtualizarPesoAreaEnemCommand>(json, Options);

        cmd.Should().NotBeNull();
        cmd!.Areas![0].Corte.Should().Be(450m);
        cmd.Areas[1].Corte.Should().BeNull();
    }

    [Fact(DisplayName = "Criar: omitir o peso de uma área no JSON é rejeitado")]
    public void Criar_SemPeso_LancaJsonException()
    {
        const string json = """
        {
          "resolucao": "Res. 805/2024", "grupoCurso": "TECNOLOGICA",
          "areas": [ { "codigo": "REDACAO", "corte": 400 } ],
          "baseLegal": "Res. 805/2024 Anexo I"
        }
        """;

        Action act = () => JsonSerializer.Deserialize<CriarPesoAreaEnemCommand>(json, Options);

        act.Should().Throw<JsonException>();
    }

    [Fact(DisplayName = "Criar: sem a lista de áreas desserializa com nulo, para a validação de domínio responder")]
    public void Criar_SemAreas_DesserializaComNulo()
    {
        const string json = """
        { "resolucao": "Res. 805/2024", "grupoCurso": "TECNOLOGICA", "baseLegal": "Res. 805/2024 Anexo I" }
        """;

        CriarPesoAreaEnemCommand? cmd = JsonSerializer.Deserialize<CriarPesoAreaEnemCommand>(json, Options);

        cmd.Should().NotBeNull();
        cmd!.Areas.Should().BeNull();
    }
}
