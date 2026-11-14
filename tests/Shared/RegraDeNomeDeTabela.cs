namespace Unifesspa.UniPlus.Testes.Compartilhado;

using System.Text.RegularExpressions;

/// <summary>
/// A regra de nome de tabela do <c>docs/guia-banco-de-dados.md</c>: <c>snake_case</c>, com o núcleo — o primeiro termo, o
/// substantivo que nomeia a linha — no plural e até <see cref="LimiteDeCaracteres"/> caracteres. O complemento fica como a
/// língua o pede (<c>aceites_do_termo</c>, <c>tipos_processo</c>). Quem aplica a regra ao catálogo e ao modelo é cada teste; esta
/// classe só decide, sem lista de exceções por tabela.
/// </summary>
/// <remarks>
/// A regra não verifica o que nenhum teste decide sem dicionário: a ausência de abreviação e a concordância de
/// adjetivos. "Termina em s" é a única marca de plural decidível, então um singular terminado em s (<c>campus</c>) passa.
/// </remarks>
internal static partial class RegraDeNomeDeTabela
{
    public const int LimiteDeCaracteres = 30;

    /// <summary>Uma linha <c>esquema.tabela: motivo</c> para cada motivo de recusa de cada tabela, em ordem.</summary>
    public static IReadOnlyList<string> Violacoes(IEnumerable<(string Esquema, string Tabela)> tabelas)
    {
        ArgumentNullException.ThrowIfNull(tabelas);

        return
        [
            .. tabelas
                .OrderBy(t => t.Esquema, StringComparer.Ordinal)
                .ThenBy(t => t.Tabela, StringComparer.Ordinal)
                .SelectMany(t => Motivos(t.Tabela).Select(motivo => $"{t.Esquema}.{t.Tabela}: {motivo}")),
        ];
    }

    public static IReadOnlyList<string> Motivos(string tabela)
    {
        ArgumentNullException.ThrowIfNull(tabela);

        return [.. MotivosDeFormato(tabela), .. MotivosDeTamanho(tabela), .. MotivosDeNucleo(tabela)];
    }

    private static IEnumerable<string> MotivosDeFormato(string tabela)
    {
        if (!SnakeCase().IsMatch(tabela))
        {
            yield return "fora do snake_case (minúsculas e dígitos, termos separados por '_')";
        }
    }

    private static IEnumerable<string> MotivosDeTamanho(string tabela)
    {
        if (tabela.Length > LimiteDeCaracteres)
        {
            yield return $"tem {tabela.Length} caracteres e o limite é {LimiteDeCaracteres}";
        }
    }

    private static IEnumerable<string> MotivosDeNucleo(string tabela)
    {
        string nucleo = tabela.Split('_')[0];
        if (!nucleo.EndsWith('s'))
        {
            yield return $"o núcleo '{nucleo}' não está no plural";
        }
    }

    [GeneratedRegex("^[a-z][a-z0-9]*(_[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SnakeCase();
}
