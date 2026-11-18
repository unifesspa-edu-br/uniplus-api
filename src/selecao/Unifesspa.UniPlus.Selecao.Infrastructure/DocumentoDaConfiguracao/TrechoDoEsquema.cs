namespace Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

/// <summary>Um trecho de um arquivo do esquema, com o lugar onde está (arquivo e JSON Pointer dentro dele).</summary>
internal sealed record TrechoDoEsquema(string Arquivo, string Ponteiro, JsonNode No)
{
    /// <summary>Identifica o trecho entre todos os arquivos do esquema.</summary>
    public string Localizacao => $"{Arquivo}#{Ponteiro}";

    public TrechoDoEsquema Filho(string passo, JsonNode no) => new(Arquivo, $"{Ponteiro}/{passo}", no);

    /// <summary>O valor da palavra-chave no trecho, ou <see langword="null"/> quando o trecho não a declara ou não é um objeto.</summary>
    public JsonNode? PalavraChave(string nome) => No is JsonObject objeto ? objeto[nome] : null;

    public bool DeclaraVerdadeiro(string nome) => PalavraChave(nome) is JsonValue valor && valor.TryGetValue(out bool declarado) && declarado;
}
