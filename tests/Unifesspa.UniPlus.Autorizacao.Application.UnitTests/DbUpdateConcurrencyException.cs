namespace Microsoft.EntityFrameworkCore;

/// <summary>
/// O handler reconhece a exceção de concorrência pelo nome completo do tipo, sem referenciar o EF Core; este tipo
/// faz o papel dela nos testes.
/// </summary>
internal sealed class DbUpdateConcurrencyException : Exception
{
}
