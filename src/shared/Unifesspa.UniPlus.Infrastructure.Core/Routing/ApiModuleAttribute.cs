namespace Unifesspa.UniPlus.Infrastructure.Core.Routing;

/// <summary>
/// Identifica um assembly de API com o nome público do módulo que ele expõe.
/// Uma única declaração no assembly atende todos os seus controllers.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class ApiModuleAttribute : Attribute
{
    public ApiModuleAttribute(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (!IsValidName(name))
        {
            throw new ArgumentException(
                "O nome do módulo deve começar com letra minúscula ASCII e conter "
                + "somente letras minúsculas ASCII, dígitos ou hífen.",
                nameof(name));
        }

        Name = name;
    }

    public string Name { get; }

    private static bool IsValidName(string name) =>
        char.IsAsciiLetterLower(name[0])
        && name.All(static character =>
            char.IsAsciiLetterLower(character)
            || char.IsAsciiDigit(character)
            || character == '-');
}
