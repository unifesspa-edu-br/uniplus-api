namespace Unifesspa.UniPlus.Infrastructure.Core.Persistence.Converters;

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

// Mapeia NomeSocial ↔ JSON (jsonb no Postgres) com payload {"nomeCivil","nome"}.
// JSON foi escolhido em vez de colunas separadas para manter o VO como um
// único campo do schema, evitando ON UPDATE em duas colunas sempre que o
// nome muda, e simplificando consultas que recuperem o registro inteiro.
// Consultas que precisem filtrar por nome civil podem usar operadores jsonb
// do Postgres (`->'nomeCivil'`) sem custo significativo em volumes do CEPS.
public sealed class NomeSocialValueConverter : ValueConverter<NomeSocial, string>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public NomeSocialValueConverter()
        : base(
            valor => Serializar(valor),
            json => Desserializar(json))
    {
    }

    private static string Serializar(NomeSocial valor)
    {
        NomeSocialPayload payload = new(valor.NomeCivil, valor.Nome);
        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static NomeSocial Desserializar(string json)
    {
        NomeSocialPayload? payload = JsonSerializer.Deserialize<NomeSocialPayload>(json, JsonOptions);

        if (payload is null)
        {
            throw new InvalidOperationException(
                "Valor JSON nulo ao desserializar NomeSocial — dado corrompido no banco.");
        }

        return ValueObjectMaterialization.Reidratar(
            NomeSocial.Criar(payload.NomeCivil, payload.Nome),
            nameof(NomeSocial));
    }

    private sealed record NomeSocialPayload(string NomeCivil, string? Nome);
}
