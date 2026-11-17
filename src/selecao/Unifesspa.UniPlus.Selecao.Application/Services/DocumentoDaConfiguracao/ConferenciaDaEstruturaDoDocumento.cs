namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Kernel.Results;

/// <summary>
/// A primeira etapa da leitura do documento: confere o texto contra os limites e a estrutura do JSON (tamanho, profundidade,
/// quantidade de valores, malformação, raiz objeto, caractere nulo em texto e propriedade repetida) e só então monta a árvore. Nada do conteúdo é
/// avaliado aqui.
/// </summary>
public static class ConferenciaDaEstruturaDoDocumento
{
    private static readonly JsonDocumentOptions OpcoesDaArvore = new() { AllowDuplicateProperties = false };

    public static Result<JsonObject> Conferir(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        if (TamanhoAcimaDoLimite(texto))
        {
            return Result<JsonObject>.ValidationFailure([CausasDaForma.TamanhoAcimaDoLimite()]);
        }

        byte[] utf8 = Encoding.UTF8.GetBytes(texto);
        IReadOnlyList<FieldError> causas = new VarreduraDaEstruturaDoDocumento(utf8).Varrer();
        return causas.Count == 0
            ? Result<JsonObject>.Success(JsonNode.Parse(utf8, nodeOptions: null, OpcoesDaArvore)!.AsObject())
            : Result<JsonObject>.ValidationFailure(causas);
    }

    /// <summary>
    /// Confere só os limites de um texto que já passou pela estrutura, sem montar a árvore: o mesmo tamanho, a mesma profundidade
    /// e a mesma quantidade de valores que o texto recebido respeitou. Devolve as causas, nenhuma quando o texto cabe.
    /// </summary>
    public static IReadOnlyList<FieldError> ConferirLimites(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        return TamanhoAcimaDoLimite(texto)
            ? [CausasDaForma.TamanhoAcimaDoLimite()]
            : new VarreduraDaEstruturaDoDocumento(Encoding.UTF8.GetBytes(texto)).Varrer();
    }

    private static bool TamanhoAcimaDoLimite(string texto) =>
        Encoding.UTF8.GetByteCount(texto) > LimitesDoDocumento.TamanhoMaximoEmBytes;
}
