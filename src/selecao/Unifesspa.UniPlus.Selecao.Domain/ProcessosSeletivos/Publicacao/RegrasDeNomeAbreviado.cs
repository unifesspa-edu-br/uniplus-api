namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;

using System.Globalization;
using System.Text;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Algoritmos de abreviação de nome para a divulgação pública (ADR-0082), por
/// <b>identificador congelado</b> — nunca a regra vigente resolvida por fora. Um edital
/// publicado com <c>nome_abreviado</c> guarda o identificador da regra que abreviou o nome
/// naquele instante; mudar o algoritmo depois não pode reinterpretar um certame já publicado.
/// </summary>
/// <remarks>
/// Cada semântica distinta de abreviação recebe um identificador NOVO — o significado do
/// identificador vigente hoje nunca muda. Se a proteção de dados ou o P.O.
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
            return Result<string>.Failure(IdentificadorDesconhecido(identificador));
        }

        // Sem separador, o Split quebra em qualquer espaço Unicode e colapsa as repetições.
        string[] tokens = nome.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        return tokens.Length == 0
            ? Result<string>.Failure(new DomainError("RegrasDeNomeAbreviado.NomeEmBranco", "O nome a abreviar não pode ser vazio ou só espaços."))
            : Result<string>.Success(AbreviarTokens(tokens));
    }

    private static DomainError IdentificadorDesconhecido(string identificador) => new(
        "RegrasDeNomeAbreviado.IdentificadorDesconhecido",
        $"O identificador de regra de abreviação '{identificador}' não é conhecido.");

    // Palavra única e sobrenome sem nome antes viram só a inicial: o nome por extenso abriria a
    // mesma exposição que o nome completo.
    private static string AbreviarTokens(string[] tokens)
    {
        if (tokens.Length == 1)
        {
            return Inicial(tokens[0]);
        }

        // O sobrenome é o último token que não é partícula, e não tokens[^1]: em "Maria Silva de"
        // o sobrenome é "Silva". As partículas depois dele são descartadas.
        int indiceSobrenome = UltimoIndiceNaoParticula(tokens);

        // Só partículas: o último token fica por extenso para não devolver texto vazio.
        return indiceSobrenome < 0
            ? $"{Iniciais(tokens[..^1])} {tokens[^1]}"
            : AbreviarComSobrenome(tokens, indiceSobrenome);
    }

    private static string AbreviarComSobrenome(string[] tokens, int indiceSobrenome)
    {
        string sobrenome = tokens[indiceSobrenome];
        string[] antesDoSobrenome = tokens[..indiceSobrenome];
        string[] nomesProprios = [.. antesDoSobrenome.Where(static t => !EhParticula(t))];

        // Em "Da Silva" só há partícula antes do sobrenome, e ela vira a inicial.
        string[] tokensDasIniciais = nomesProprios.Length > 0 ? nomesProprios : antesDoSobrenome;

        return tokensDasIniciais.Length > 0 ? $"{Iniciais(tokensDasIniciais)} {sobrenome}" : Inicial(sobrenome);
    }

    private static string Inicial(string token) => $"{PrimeiroElementoDeTexto(token)}.";

    private static string Iniciais(IEnumerable<string> tokens) => string.Join(" ", tokens.Select(Inicial));

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
