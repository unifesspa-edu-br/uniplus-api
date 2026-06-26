namespace Unifesspa.UniPlus.Infrastructure.Core.Messaging.Middleware;

using FluentValidation;
using FluentValidation.Results;

using Wolverine.FluentValidation;

/// <summary>
/// <see cref="IFailureAction{T}"/> do pipeline FluentValidation do Wolverine que
/// preserva a invariante de masking de PII do UniPlus (LGPD, Parecer DPO 002/2026):
/// lança <see cref="ValidationException"/> apenas com as falhas de regra, SEM
/// serializar o command na mensagem da exceção.
/// </summary>
/// <remarks>
/// <para>O <c>FailureAction&lt;T&gt;</c> default do pacote lança
/// <c>new ValidationException($"Validation failure on: {message}", failures)</c>.
/// Para um command do tipo <c>record</c>, <c>{message}</c> chama o
/// <c>ToString()</c> sintetizado — que expõe TODOS os campos (CPF, CNPJ, nome,
/// endereço) na <see cref="System.Exception.Message"/>. O
/// <c>GlobalExceptionMiddleware</c> registra essa exceção em log, o que vazaria
/// PII de candidato no log estruturado (o <c>PiiMaskingEnricher</c> mascara
/// propriedades estruturadas, não o texto livre de <c>Exception.ToString()</c>).</para>
/// <para>Esta implementação replica o comportamento do antigo
/// <c>WolverineValidationMiddleware</c> (<c>throw new ValidationException(failures)</c>):
/// a mensagem fica restrita às falhas de regra (sanitizadas — desde que os
/// templates FluentValidation não usem <c>{PropertyValue}</c>, invariante já
/// documentada no <c>GlobalExceptionMiddleware</c>). É registrada como open-generic
/// Singleton ANTES de <c>UseFluentValidation</c>, que usa <c>TryAddSingleton</c> —
/// então esta implementação tem precedência sobre o default.</para>
/// <para><c>public</c> de propósito: o code-gen do Wolverine injeta o
/// <see cref="IFailureAction{T}"/> resolvido na chain; um tipo <c>internal</c>
/// reintroduziria o warning "not public, requires service location" que esta
/// adoção do pacote justamente elimina.</para>
/// </remarks>
/// <typeparam name="T">Tipo da mensagem (command/query) validada.</typeparam>
public sealed class PiiSafeValidationFailureAction<T> : IFailureAction<T>
{
    /// <inheritdoc />
    public void Throw(T message, IReadOnlyList<ValidationFailure> failures)
        => throw new ValidationException(failures);
}
