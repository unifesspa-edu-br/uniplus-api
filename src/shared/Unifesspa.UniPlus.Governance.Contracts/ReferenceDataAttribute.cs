namespace Unifesspa.UniPlus.Governance.Contracts;

/// <summary>
/// Marca uma entidade de domínio como dado de referência área-scoped. Não tem
/// comportamento em runtime — é consumido pelos fitness tests ArchUnitNET
/// (F1.S3) que garantem que toda entidade marcada tem uma
/// <c>AreaVisibilityConfiguration</c> correspondente (ADR-0060).
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ReferenceDataAttribute : Attribute;
