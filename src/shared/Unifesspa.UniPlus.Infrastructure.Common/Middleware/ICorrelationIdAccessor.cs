namespace Unifesspa.UniPlus.Infrastructure.Common.Middleware;

// Contract somente-leitura exposto a qualquer consumer do DI. Separar da
// capacidade de escrita (ICorrelationIdWriter) previne que handlers
// downstream sobrescrevam o ID do request em andamento por acidente.
public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; }
}
