namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Globalization;
using System.Text;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Algoritmos de abreviação de nome para a divulgação pública (ADR-0082, issue #563), por
/// <b>identificador congelado</b> — nunca a regra vigente resolvida por fora. Um edital
/// publicado com <c>nome_abreviado</c> guarda o identificador da regra que abreviou o nome
/// naquele instante; mudar o algoritmo depois não pode reinterpretar um certame já publicado.
/// </summary>
/// <remarks>
/// Cada semântica distinta de abreviação recebe um identificador NOVO — o significado do
/// identificador vigente hoje nunca muda. Se a Encarregada de Proteção de Dados ou o P.O.
/// divergirem de qualquer regra abaixo, a resposta é um rótulo novo, nunca uma reinterpretação
/// deste.
/// </remarks>
public static class RegrasDeNomeAbreviado
{
    /// <summary>
    /// A regra vigente: iniciais dos primeiros nomes seguidas do último sobrenome por extenso
    /// (ADR-0082, exemplo normativo "Maria Lima Almeida" → "M. L. Almeida").
    /// </summary>
    public const string Vigente = "iniciais_mais_ultimo_sobrenome";

    private static readonly string[] Particulas =
    [
        "de", "da", "do", "das", "dos", "e", "di", "du", "del", "van", "von",
    ];

    public static bool EhConhecida(string identificador) =>
        string.Equals(identificador, Vigente, StringComparison.Ordinal);

    /// <summary>
    /// Aplica a regra identificada a <paramref name="nome"/>. Um identificador que
    /// <see cref="EhConhecida"/> não reconheça é <b>recusado</b> — nunca resolvido pela regra
    /// vigente, que reinterpretaria retroativamente um rótulo congelado.
    /// </summary>
    public static Result<string> Abreviar(string identificador, string nome)
    {
        ArgumentNullException.ThrowIfNull(nome);

        if (!EhConhecida(identificador))
        {
            return Result<string>.Failure(new DomainError(
                "RegrasDeNomeAbreviado.IdentificadorDesconhecido",
                $"O identificador de regra de abreviação '{identificador}' não é conhecido."));
        }

        // Espaço em branco Unicode, colapsando repetições — a sobrecarga sem separador
        // explícito do runtime já trata qualquer caractere que char.IsWhiteSpace reconheça.
        string[] tokens = nome.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return Result<string>.Failure(new DomainError(
                "RegrasDeNomeAbreviado.NomeEmBranco",
                "O nome a abreviar não pode ser vazio ou só espaços."));
        }

        // Palavra única: nunca o nome inteiro — abriria a mesma exposição que "nome" (sem a
        // justificativa que ele exige). É o caso do nome social de uma só palavra.
        if (tokens.Length == 1)
        {
            return Result<string>.Success($"{PrimeiroElementoDeTexto(tokens[0])}.");
        }

        // O sobrenome é o ÚLTIMO token que não é partícula — nunca tokens[^1] direto: "Maria
        // Silva de" tem partícula na cauda, e o sobrenome é "Silva", não "de".
        int indiceSobrenome = UltimoIndiceNaoParticula(tokens);

        if (indiceSobrenome < 0)
        {
            // Entrada só de partículas: nenhum token sobra como sobrenome "de verdade". Mantém
            // o mesmo tratamento de quando havia um sobrenome não-partícula — o último token
            // por extenso, os anteriores como inicial — para não devolver string vazia.
            string sobrenomeDegenerado = tokens[^1];
            string iniciaisDegeneradas = string.Join(
                " ", tokens[..^1].Select(static t => $"{PrimeiroElementoDeTexto(t)}."));
            return Result<string>.Success($"{iniciaisDegeneradas} {sobrenomeDegenerado}");
        }

        // Tokens depois do sobrenome são partícula final (por construção de
        // UltimoIndiceNaoParticula) e são simplesmente ignorados — é o que descarta o "de" de
        // "Maria Silva de" em vez de congelá-lo como se fosse o sobrenome.
        string sobrenome = tokens[indiceSobrenome];
        string[] tokensAntes = tokens[..indiceSobrenome];
        string[] iniciaisReais = [.. tokensAntes.Where(static t => !EhParticula(t))];

        if (iniciaisReais.Length > 0)
        {
            string iniciais = string.Join(" ", iniciaisReais.Select(static t => $"{PrimeiroElementoDeTexto(t)}."));
            return Result<string>.Success($"{iniciais} {sobrenome}");
        }

        if (tokensAntes.Length > 0)
        {
            // Todos os tokens antes do sobrenome eram partícula ("Da Silva"): usa-os como
            // vieram em vez de descartá-los, senão não sobraria nenhuma inicial.
            string iniciaisDeParticulas = string.Join(
                " ", tokensAntes.Select(static t => $"{PrimeiroElementoDeTexto(t)}."));
            return Result<string>.Success($"{iniciaisDeParticulas} {sobrenome}");
        }

        // Não há token nenhum antes do sobrenome ("Maria de", onde "de" é descartado e "Maria"
        // é o sobrenome): ele é o único conteúdo do nome, e vira só a inicial — nunca por
        // extenso, mesmo raciocínio da palavra única.
        return Result<string>.Success($"{PrimeiroElementoDeTexto(sobrenome)}.");
    }

    /// <summary>O índice do último token que NÃO é partícula, ou -1 se todos forem.</summary>
    private static int UltimoIndiceNaoParticula(string[] tokens)
    {
        for (int i = tokens.Length - 1; i >= 0; i--)
        {
            if (!EhParticula(tokens[i]))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Partículas comparadas sem diferenciar caixa nem acento — "de" não é um "primeiro nome" (ADR-0082).</summary>
    private static bool EhParticula(string token) =>
        Array.IndexOf(Particulas, RemoverAcentos(token).ToLowerInvariant()) >= 0;

    private static string RemoverAcentos(string valor) =>
        string.Concat(valor.Normalize(NormalizationForm.FormD)
            .Where(static c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));

    /// <summary>
    /// O primeiro ELEMENTO DE TEXTO Unicode do token, maiúsculo — nunca <c>token[0]</c>: um
    /// grafema composto por mais de um <see cref="char"/> (par substituto, letra com
    /// combinante) seria partido ao meio por indexação bruta.
    /// </summary>
    private static string PrimeiroElementoDeTexto(string token)
    {
        TextElementEnumerator enumerador = StringInfo.GetTextElementEnumerator(token);
        enumerador.MoveNext();
        return ((string)enumerador.Current).ToUpperInvariant();
    }
}
