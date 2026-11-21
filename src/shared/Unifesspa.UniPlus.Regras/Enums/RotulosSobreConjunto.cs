namespace Unifesspa.UniPlus.Regras.Enums;

using System.Collections.Frozen;
using System.Reflection;

/// <summary>Os rótulos que os membros de <see cref="Operador"/> declararam em <see cref="RotuloSobreConjuntoAttribute"/>, lidos uma vez.</summary>
internal static class RotulosSobreConjunto
{
    private static readonly Lazy<FrozenDictionary<Operador, string>> Declarados = new(Ler, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>O rótulo do operador sobre conjunto, ou nulo quando o operador não vale sobre conjunto.</summary>
    public static string? De(Operador operador) => Declarados.Value.GetValueOrDefault(operador);

    private static FrozenDictionary<Operador, string> Ler() =>
        typeof(Operador)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static campo => campo.IsDefined(typeof(RotuloSobreConjuntoAttribute), inherit: false))
            .ToFrozenDictionary(
                static campo => Enum.Parse<Operador>(campo.Name),
                static campo => campo.GetCustomAttribute<RotuloSobreConjuntoAttribute>()!.Rotulo);
}
