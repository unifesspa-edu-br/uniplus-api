namespace Unifesspa.UniPlus.Infrastructure.Core.Middleware;

// Contract de escrita, injetado apenas no CorrelationIdMiddleware.
// Estende o reader para permitir que o próprio middleware leia de volta
// o valor (útil em testes e composições).
public interface ICorrelationIdWriter : ICorrelationIdAccessor
{
    void SetCorrelationId(string correlationId);
}
