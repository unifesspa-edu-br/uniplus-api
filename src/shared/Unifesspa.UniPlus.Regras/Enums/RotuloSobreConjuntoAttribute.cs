namespace Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// O rótulo que o operador tem quando o fato do candidato é um conjunto de valores. O rótulo de valor único é o que o
/// membro declara em <c>CodigoDeContratoAttribute</c>; o operador que não vale sobre conjunto não declara este.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
public sealed class RotuloSobreConjuntoAttribute : Attribute
{
    /// <summary>Declara o rótulo do operador sobre conjunto.</summary>
    /// <param name="rotulo">O texto que a pessoa lê, em pt-BR.</param>
    public RotuloSobreConjuntoAttribute(string rotulo)
    {
        Rotulo = rotulo;
    }

    /// <summary>O texto que a pessoa lê quando o fato é um conjunto.</summary>
    public string Rotulo { get; }
}
