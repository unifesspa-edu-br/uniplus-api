namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Recusa <c>null</c> dentro de qualquer arranjo do documento divulgado, em qualquer profundidade.
/// </summary>
/// <remarks>
/// <para>
/// <c>RespectNullableAnnotations</c> alcança o MEMBRO — <c>"recursos": null</c> —, não o ELEMENTO:
/// <c>"recursos": [null]</c> desserializa sem queixa e deixa um nulo onde o contrato declara texto.
/// A resposta então sai com 200 e uma entrada nula no arranjo, e quem a percorre estoura.
/// </para>
/// <para>
/// Conferir isso depois, descendo aos arranjos aninhados à mão, é manter uma segunda cópia da forma
/// do contrato: ela envelhece a cada arranjo novo, e o arranjo que ninguém lembrou de incluir passa
/// em silêncio. Aqui a recusa é da desserialização, e alcança o grafo inteiro sem enumerá-lo.
/// </para>
/// <para>
/// Só arranjo de tipo de referência: <c>IReadOnlyList&lt;Guid&gt;</c> já recusa o nulo por ser
/// arranjo de valor.
/// </para>
/// </remarks>
internal sealed class ArranjoSemElementoNulo : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        return typeToConvert.IsGenericType
            && typeToConvert.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)
            && !typeToConvert.GetGenericArguments()[0].IsValueType;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        Type elemento = typeToConvert.GetGenericArguments()[0];
        return (JsonConverter)Activator.CreateInstance(typeof(Conversor<>).MakeGenericType(elemento))!;
    }

    private sealed class Conversor<T> : JsonConverter<IReadOnlyList<T>>
        where T : class
    {
        public override IReadOnlyList<T>? Read(
            ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                // O arranjo AUSENTE ou nulo é assunto das anotações de nulidade do membro, que já
                // recusam onde o contrato não admite. Aqui só o elemento é conferido.
                return null;
            }

            if (reader.TokenType != JsonTokenType.StartArray)
            {
                throw new JsonException($"Esperado arranjo para {typeToConvert.Name}.");
            }

            List<T> itens = [];
            while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
            {
                itens.Add(
                    JsonSerializer.Deserialize<T>(ref reader, options)
                    ?? throw new JsonException($"Arranjo de {typeof(T).Name} tem elemento nulo."));
            }

            return itens;
        }

        /// <summary>
        /// Grava pelo tipo de sequência, e não pelo de arranjo, para não reentrar nesta fábrica.
        /// </summary>
        public override void Write(Utf8JsonWriter writer, IReadOnlyList<T> value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, (IEnumerable<T>)value, options);
    }
}
