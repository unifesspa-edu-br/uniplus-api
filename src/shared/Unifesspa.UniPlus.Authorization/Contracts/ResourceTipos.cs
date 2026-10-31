namespace Unifesspa.UniPlus.Authorization.Contracts;

using System.Text.RegularExpressions;

/// <summary>
/// Vocabulário do tipo de resource que a decisão de acesso enxerga: o formato que todo tipo segue e o tipo fixo das
/// decisões sem alvo próprio. Os tipos de cada módulo ficam numa classe do próprio módulo.
/// </summary>
public static partial class ResourceTipos
{
    /// <summary>
    /// Sem alvo específico: o alvo é a própria operação, como cadastrar algo que ainda não tem id ou conferir se a
    /// pessoa pode agir em alguma unidade para mostrar um botão.
    /// </summary>
    public const string Operacao = "operacao";

    /// <summary>Se o tipo segue a convenção: kebab-case, em minúsculas e dígitos, sem acento.</summary>
    public static bool SegueAConvencao(string? tipo) => tipo is not null && FormatoDoTipo().IsMatch(tipo);

    [GeneratedRegex("^" + SegmentoKebab.Padrao + @"\z", RegexOptions.CultureInvariant)]
    private static partial Regex FormatoDoTipo();
}
