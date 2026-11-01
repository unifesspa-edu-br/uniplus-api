namespace Unifesspa.UniPlus.Authorization.Contracts;

using System.Text.Json;
using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Authorization.Enums;

/// <summary>
/// Grava o resultado no valor canônico (<c>permitido</c>, <c>negado</c>, <c>impedido</c>), como o registro grava os
/// demais enums, e não no identificador C#. O registro só é escrito por este processo; ninguém o lê de volta.
/// </summary>
internal sealed class ResultadoDoAcessoCanonicoConverter : JsonConverter<ResultadoDoAcesso>
{
    public override ResultadoDoAcesso Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("O resultado do registro de acesso só é escrito.");

    public override void Write(Utf8JsonWriter writer, ResultadoDoAcesso value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(ValoresCanonicos.De(value));
    }
}
