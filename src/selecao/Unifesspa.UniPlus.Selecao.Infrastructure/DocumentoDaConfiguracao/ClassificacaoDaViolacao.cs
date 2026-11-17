namespace Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using Json.Schema;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

/// <summary>
/// Dá nome à violação que a avaliação reporta: propriedade não declarada, tipo, valor (enumeração, valor fixo, padrão, limites),
/// campo obrigatório ausente e versão do formato não suportada. A mensagem descreve a regra e o tipo esperado, nunca o valor.
/// </summary>
internal sealed class ClassificacaoDaViolacao(ArquivosDoEsquema arquivos, JsonObject documento)
{
    private static readonly string[] PalavrasDePropriedadeNaoDeclarada = ["additionalProperties", "unevaluatedProperties"];

    private const string CampoNaoPermitido = "o campo não é permitido nas condições do documento";

    private static readonly Dictionary<string, string> RegrasDosValores = new(StringComparer.Ordinal)
    {
        ["const"] = "deve ter o valor fixo do formato",
        ["enum"] = "deve ser um dos valores permitidos",
        ["pattern"] = "deve respeitar o padrão do campo",
        ["format"] = "deve respeitar o formato do campo",
        ["minLength"] = "o tamanho do texto está fora do limite permitido",
        ["maxLength"] = "o tamanho do texto está fora do limite permitido",
        ["minimum"] = "o número está fora do limite permitido",
        ["maximum"] = "o número está fora do limite permitido",
        ["exclusiveMinimum"] = "o número está fora do limite permitido",
        ["exclusiveMaximum"] = "o número está fora do limite permitido",
        ["multipleOf"] = "deve respeitar a precisão permitida",
        ["minItems"] = "a quantidade de itens está fora do limite permitido",
        ["maxItems"] = "a quantidade de itens está fora do limite permitido",
        ["uniqueItems"] = "os itens da lista não podem se repetir",
    };

    public IEnumerable<CausaClassificada> Classificar(EvaluationResults violacao)
    {
        string ponteiro = violacao.InstanceLocation.ToString();
        return violacao.Errors is { Count: > 0 } palavras
            ? palavras.Keys.SelectMany(palavra => Classificar(violacao, ponteiro, palavra))
            : SemPalavraDeErro(violacao, ponteiro);
    }

    // O esquema falso não nomeia palavra-chave: é a propriedade que o objeto não declara (additionalProperties) ou o campo que
    // um ramo condicional proíbe.
    private static IEnumerable<CausaClassificada> SemPalavraDeErro(EvaluationResults violacao, string ponteiro) =>
        EhPropriedadeNaoDeclarada(violacao)
            ? [new(0, CausasDaForma.CampoDesconhecido(ponteiro))]
            : [new(3, CausasDaForma.ValorInvalido(ponteiro, CampoNaoPermitido))];

    private IEnumerable<CausaClassificada> Classificar(EvaluationResults violacao, string ponteiro, string palavra) => palavra switch
    {
        "type" => [new(1, CausasDaForma.TipoInvalido(ponteiro, TiposDoEsquema.Esperado(arquivos.Localizar(violacao.SchemaLocation))))],
        "required" => Ausentes(violacao, ponteiro),
        "" => SemPalavraDeErro(violacao, ponteiro),
        "const" when PonteiroJson.Segmentos(ponteiro).Count == 1 => [new(2, CausasDaForma.VersaoDoFormatoNaoSuportada(ponteiro))],
        _ => [new(3, CausasDaForma.ValorInvalido(ponteiro, RegrasDosValores.GetValueOrDefault(palavra, "não atende a uma regra do formato")))],
    };

    private static bool EhPropriedadeNaoDeclarada(EvaluationResults violacao) =>
        PalavrasDePropriedadeNaoDeclarada.Any(palavra => violacao.EvaluationPath.ToString().EndsWith($"/{palavra}", StringComparison.Ordinal));

    private IEnumerable<CausaClassificada> Ausentes(EvaluationResults violacao, string ponteiro)
    {
        JsonObject? objeto = PonteiroJson.Resolver(documento, ponteiro) as JsonObject;
        IEnumerable<string> exigidas = arquivos.Localizar(violacao.SchemaLocation).PalavraChave("required") is JsonArray lista
            ? lista.OfType<JsonValue>().Select(static valor => valor.GetValue<string>())
            : [];

        return exigidas.Where(nome => objeto is not null && !objeto.ContainsKey(nome))
            .Select(nome => new CausaClassificada(4, CausasDaForma.CampoObrigatorioAusente(PonteiroJson.Acrescentar(ponteiro, nome))));
    }
}
