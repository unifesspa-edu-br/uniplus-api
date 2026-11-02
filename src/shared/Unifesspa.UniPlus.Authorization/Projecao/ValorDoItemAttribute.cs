namespace Unifesspa.UniPlus.Authorization.Projecao;

/// <summary>
/// Marca a propriedade que guarda o valor de um item classificado. O valor sai só quando o par do item foi permitido
/// (<see cref="PermissoesDaProjecaoExtensions.ValorSai"/>), mesmo quando o item aparece pela capacidade; sem ele, o
/// serializador omite a propriedade (ADR-0153).
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ValorDoItemAttribute : Attribute
{
}
