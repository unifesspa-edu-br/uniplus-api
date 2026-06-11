namespace Unifesspa.UniPlus.Governance.Contracts;

/// <summary>
/// Marca uma entidade de domínio como dado de referência. Não tem
/// comportamento em runtime — é consumido pelos fitness tests ArchUnitNET.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ReferenceDataAttribute : Attribute;
