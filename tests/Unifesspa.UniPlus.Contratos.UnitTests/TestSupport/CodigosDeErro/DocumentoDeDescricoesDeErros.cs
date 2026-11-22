namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport.CodigosDeErro;

using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

internal sealed record DocumentoDeDescricoesDeErros(int VersaoDoFormato, IReadOnlyList<DescricaoDeErro?>? Descricoes)
{
    internal const int VersaoAtual = 1;

    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        NumberHandling = JsonNumberHandling.Strict,
    };

    internal static DocumentoDeDescricoesDeErros Ler(string conteudo) =>
        JsonSerializer.Deserialize<DocumentoDeDescricoesDeErros>(conteudo, Opcoes)
        ?? throw new JsonException("Falta o documento de descrições.");

    internal string SerializarEmOrdem()
    {
        DocumentoDeDescricoesDeErros ordenado = this with
        {
            Descricoes = Descricoes?.OrderBy(static descricao => descricao?.Codigo, StringComparer.Ordinal).ToArray(),
        };
        return JsonSerializer.Serialize(ordenado, Opcoes) + "\n";
    }
}
