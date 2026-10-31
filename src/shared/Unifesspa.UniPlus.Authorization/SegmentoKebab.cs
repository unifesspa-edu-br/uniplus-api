namespace Unifesspa.UniPlus.Authorization;

/// <summary>
/// Padrão do segmento em kebab-case, em minúsculas e dígitos, sem acento: a forma única dos tipos de resource e de
/// cada parte do código de permissão.
/// </summary>
internal static class SegmentoKebab
{
    public const string Padrao = "[a-z0-9]+(-[a-z0-9]+)*";
}
