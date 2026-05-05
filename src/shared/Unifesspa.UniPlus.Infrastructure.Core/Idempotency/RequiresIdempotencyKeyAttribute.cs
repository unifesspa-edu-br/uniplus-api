namespace Unifesspa.UniPlus.Infrastructure.Core.Idempotency;

/// <summary>
/// Sinaliza que o endpoint exige header <c>Idempotency-Key</c> (ADR-0027).
/// Aplicado em métodos de controllers que executam comandos críticos
/// (POST/PATCH não-idempotentes por semântica). O <c>IdempotencyFilter</c>
/// detecta o atributo via metadata da action, valida o header, faz
/// lookup/store e short-circuit em caso de replay ou body mismatch.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RequiresIdempotencyKeyAttribute : Attribute;
