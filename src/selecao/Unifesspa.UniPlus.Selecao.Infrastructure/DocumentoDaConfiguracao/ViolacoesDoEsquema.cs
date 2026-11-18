namespace Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using Json.Schema;

using Kernel.Results;

/// <summary>
/// Converte o resultado da avaliação em causas: uma por ponteiro, a de maior precedência. O que falha sob <c>not</c> ou sob
/// a condição <c>if</c> não é violação do documento, é a avaliação escolhendo o ramo, e fica de fora.
/// </summary>
internal static class ViolacoesDoEsquema
{
    private static readonly string[] PalavrasQueAbremNome = ["properties", "patternProperties", "$defs"];
    private static readonly string[] PalavrasDeCondicaoOuNegacao = ["if", "not"];

    public static IReadOnlyList<FieldError> Causas(EvaluationResults resultado, ArquivosDoEsquema arquivos, JsonObject documento)
    {
        ClassificacaoDaViolacao classificacao = new(arquivos, documento);
        return
        [
            .. Folhas(resultado)
                .SelectMany(classificacao.Classificar)
                .GroupBy(static causa => causa.Causa.Field, StringComparer.Ordinal)
                .Select(static grupo => grupo.MinBy(static causa => causa.Precedencia).Causa),
        ];
    }

    // Desce só pelo que foi recusado: a ramificação que falhou sob um ponto aceito (uma alternativa de anyOf que perdeu para
    // outra, por exemplo) não é violação. O ponto que não nomeia palavra de erro e tem recusa abaixo só agrega.
    private static List<EvaluationResults> Folhas(EvaluationResults ponto)
    {
        if (ponto.IsValid || EstaSobCondicaoOuNegacao(ponto))
        {
            return [];
        }

        List<EvaluationResults> abaixo = [.. (ponto.Details ?? []).SelectMany(Folhas)];
        return ponto.Errors is { Count: > 0 } || abaixo.Count == 0 ? [ponto, .. abaixo] : abaixo;
    }

    // O caminho de avaliação alterna palavra-chave e, depois de "properties", o nome da propriedade: só a palavra conta.
    private static bool EstaSobCondicaoOuNegacao(EvaluationResults violacao)
    {
        bool nomeDePropriedade = false;
        foreach (string passo in violacao.EvaluationPath.ToString().Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!nomeDePropriedade && PalavrasDeCondicaoOuNegacao.Contains(passo, StringComparer.Ordinal))
            {
                return true;
            }

            nomeDePropriedade = !nomeDePropriedade && PalavrasQueAbremNome.Contains(passo, StringComparer.Ordinal);
        }

        return false;
    }
}
