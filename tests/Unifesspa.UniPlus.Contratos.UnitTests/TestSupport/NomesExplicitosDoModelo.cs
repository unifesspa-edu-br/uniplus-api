namespace Unifesspa.UniPlus.Contratos.UnitTests.TestSupport;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// Confere, no modelo, que a chave estrangeira e o índice de um tipo de entidade têm o nome configurado de forma explícita
/// (<c>HasConstraintName</c> e <c>HasDatabaseName</c>) e não derivado pela convenção. A prova é a origem da configuração do nome:
/// um nome derivado, mesmo igual ao que a convenção produziria, tem origem de convenção.
/// </summary>
internal static class NomesExplicitosDoModelo
{
    internal static IReadOnlyList<string> Violacoes(IEnumerable<IEntityType> tipos) =>
        [.. tipos.SelectMany(tipo => ChavesEstrangeirasSemNomeExplicito(tipo).Concat(IndicesSemNomeExplicito(tipo)))];

    private static IEnumerable<string> ChavesEstrangeirasSemNomeExplicito(IEntityType tipo) =>
        tipo.GetForeignKeys().Cast<IConventionForeignKey>()
            .Where(chave => chave.GetConstraintNameConfigurationSource() != ConfigurationSource.Explicit)
            .Select(chave => $"{tipo.DisplayName()}: a chave estrangeira '{chave.GetConstraintName()}' não tem o nome configurado explicitamente");

    private static IEnumerable<string> IndicesSemNomeExplicito(IEntityType tipo) =>
        tipo.GetIndexes().Cast<IConventionIndex>()
            .Where(indice => indice.GetDatabaseNameConfigurationSource() != ConfigurationSource.Explicit)
            .Select(indice => $"{tipo.DisplayName()}: o índice '{indice.GetDatabaseName()}' não tem o nome configurado explicitamente");
}
