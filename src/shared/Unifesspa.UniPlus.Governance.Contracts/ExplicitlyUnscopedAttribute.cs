namespace Unifesspa.UniPlus.Governance.Contracts;

/// <summary>
/// Marca um método que opera deliberadamente sem o filtro de áreas (ADR-0057
/// Pattern 4) — por exemplo, jobs noturnos com escopo <c>plataforma-admin</c>.
/// O <see cref="Reason"/> é obrigatório: documenta por que o escopo de áreas
/// não se aplica ali. Um fitness test ArchUnitNET (F1.S3) valida que todo
/// call site assim assegura o role <c>plataforma-admin</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ExplicitlyUnscopedAttribute : Attribute
{
    /// <param name="reason">
    /// Justificativa de por que este call site opera sem filtro de áreas.
    /// </param>
    public ExplicitlyUnscopedAttribute(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        Reason = reason;
    }

    /// <summary>Justificativa de por que o filtro de áreas não se aplica.</summary>
    public string Reason { get; }
}
