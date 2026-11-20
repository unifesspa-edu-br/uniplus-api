namespace Unifesspa.UniPlus.Infrastructure.Core.OpenApi;

using System.Collections.Concurrent;
using System.Text.Json.Serialization.Metadata;

using Microsoft.AspNetCore.OpenApi;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;

/// <summary>
/// Dá o nome de cada componente do documento OpenAPI. O código com rótulo de um enum leva o nome do enum, sem o tipo
/// genérico; os demais tipos ficam no nome padrão. O tipo e a sua versão anulável são o mesmo tipo. Dois tipos diferentes
/// nunca recebem o mesmo nome: a geração do documento falha, em vez de publicar um componente que serve a tipos
/// diferentes.
/// </summary>
internal sealed class NomesDosComponentes
{
    private readonly ConcurrentDictionary<string, Type> _donos = new(StringComparer.Ordinal);

    public string? Nome(JsonTypeInfo tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        string? nome = EhCodigoComRotulo(tipo.Type)
            ? tipo.Type.GetGenericArguments()[0].Name
            : OpenApiOptions.CreateDefaultSchemaReferenceId(tipo);

        if (nome is not null)
        {
            ExigirNomeDeUmSoTipo(nome, Nullable.GetUnderlyingType(tipo.Type) ?? tipo.Type);
        }

        return nome;
    }

    private static bool EhCodigoComRotulo(Type tipo) =>
        tipo is { IsGenericType: true } && tipo.GetGenericTypeDefinition() == typeof(CodigoComRotulo<>);

    private void ExigirNomeDeUmSoTipo(string nome, Type tipo)
    {
        Type dono = _donos.GetOrAdd(nome, tipo);

        if (dono != tipo)
        {
            throw new InvalidOperationException(
                $"O componente '{nome}' do documento OpenAPI serviria a dois tipos diferentes: {dono.FullName} e {tipo.FullName}. "
                + "Dê nomes diferentes aos tipos ou aos enums.");
        }
    }
}
