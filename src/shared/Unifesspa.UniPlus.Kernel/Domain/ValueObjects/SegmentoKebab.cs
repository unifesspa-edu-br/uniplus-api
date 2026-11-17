namespace Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

/// <summary>
/// Padrão do segmento em kebab-case, em minúsculas e dígitos, sem acento: a forma única dos tipos de resource e de
/// cada parte do código de permissão. Aceita começar por dígito, ao contrário do <see cref="FormatoKebab"/>, que também
/// limita o tamanho.
/// </summary>
public static class SegmentoKebab
{
    public const string Padrao = "[a-z0-9]+(-[a-z0-9]+)*";
}
