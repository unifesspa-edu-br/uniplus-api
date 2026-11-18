namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using Kernel.Results;

/// <summary>
/// A primeira etapa da leitura do documento: confere o texto contra os limites e a estrutura do JSON (tamanho, profundidade,
/// teto de valores do texto recebido, malformação, raiz objeto, caractere nulo em texto e propriedade repetida) e só então
/// monta a árvore. Nada do conteúdo é avaliado aqui. O limite de valores do documento declarado, sem os campos só de leitura
/// que a exportação acrescenta, se confere na árvore já normalizada.
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
    /// Confere só os limites de um texto que já passou pela estrutura, sem montar a árvore: o mesmo tamanho e a mesma
    /// profundidade que o texto recebido respeitou, e o mesmo teto de valores. Devolve as causas, nenhuma quando o texto cabe.
    /// </summary>
    public static IReadOnlyList<FieldError> ConferirLimites(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        return TamanhoAcimaDoLimite(texto)
            ? [CausasDaForma.TamanhoAcimaDoLimite()]
            : new VarreduraDaEstruturaDoDocumento(Encoding.UTF8.GetBytes(texto)).Varrer();
    }

    /// <summary>
    /// Conta os valores, objetos e listas do documento declarado, a árvore já sem os campos só de leitura que o sistema define e
    /// a importação ignora, e recusa o que passa do limite.
    /// </summary>
    public static IReadOnlyList<FieldError> ConferirQuantidadeDeNos(JsonObject documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        return QuantidadeDeNos(documento) > LimitesDoDocumento.QuantidadeMaximaDeNos
            ? [CausasDaForma.QuantidadeDeNosAcimaDoLimite()]
            : [];
    }

    private static int QuantidadeDeNos(JsonNode? no) => no switch
    {
        JsonObject objeto => 1 + objeto.Sum(static propriedade => QuantidadeDeNos(propriedade.Value)),
        JsonArray lista => 1 + lista.Sum(static item => QuantidadeDeNos(item)),
        _ => 1,
    };

    private static bool TamanhoAcimaDoLimite(string texto) =>
        Encoding.UTF8.GetByteCount(texto) > LimitesDoDocumento.TamanhoMaximoEmBytes;
}
