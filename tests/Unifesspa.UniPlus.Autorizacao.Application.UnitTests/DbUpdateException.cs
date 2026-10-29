namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// O handler reconhece a violação de índice único pelo nome completo do tipo e pelo estado SQL e nome da
/// restrição da exceção interna, sem referenciar o EF Core; este tipo faz o papel dela nos testes.
/// </summary>
internal sealed class DbUpdateException(string message, Exception innerException) : Exception(message, innerException)
{
}
