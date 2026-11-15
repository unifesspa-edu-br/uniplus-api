namespace Unifesspa.UniPlus.ArchTests.TestSupport.CodigosDeErro;

using System.Text.Encodings.Web;
using System.Text.Json;

/// <summary>
/// O conteúdo de <c>contracts/codigos-de-erro.json</c>: a versão do formato e a lista de códigos,
/// ordenada por código. Quem lê o arquivo (o lint do catálogo de erros do portal) confere a versão
/// antes de interpretar a lista.
/// </summary>
internal sealed record DocumentoDoCatalogoDeErros(int VersaoDoFormato, IReadOnlyList<EntradaDoCatalogoDeErros> Codigos)
{
    internal const int VersaoAtualDoFormato = 1;

    // O encoder relaxado mantém os acentos do título legíveis no arquivo, em vez de \uXXXX.
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    internal static DocumentoDoCatalogoDeErros Gerar(IReadOnlyList<EntradaDoCatalogoDeErros> codigos) =>
        new(VersaoAtualDoFormato, codigos);

    internal static DocumentoDoCatalogoDeErros Ler(string json) =>
        JsonSerializer.Deserialize<DocumentoDoCatalogoDeErros>(json, Opcoes)
        ?? throw new JsonException("O arquivo de códigos de erro está vazio.");

    internal string Serializar() => JsonSerializer.Serialize(this, Opcoes) + "\n";
}
