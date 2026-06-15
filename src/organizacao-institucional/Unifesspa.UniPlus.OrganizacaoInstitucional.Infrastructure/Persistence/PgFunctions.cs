namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

// Stubs mapeados como DB functions no OrganizacaoInstitucionalDbContext via
// HasDbFunction. O EF Core requer que o tipo declarante seja public para que
// o tradutor LINQ-to-SQL reconheça as chamadas na árvore de expressão.
// Nunca são chamados em C# — o stub sempre lança para detectar uso acidental.
public static class PgFunctions
{
    // Mapeia para immutable_unaccent(text) criada pela migration
    // AddSearchExtensionsGin. Remove diacríticos de forma IMMUTABLE,
    // viabilizando índices GIN de expressão sobre campos de texto.
    public static string ImmutableUnaccent(string? text) =>
        throw new InvalidOperationException(
            "PgFunctions.ImmutableUnaccent é um stub de EF Core e não pode ser chamado diretamente.");
}
