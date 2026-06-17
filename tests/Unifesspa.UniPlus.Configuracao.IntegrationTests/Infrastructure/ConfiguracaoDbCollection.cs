namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Coleção que compartilha um único <see cref="ConfiguracaoDbFixture"/> (Postgres
/// efêmero) entre as suítes de persistência de Campus e LocalOferta — evita subir
/// um container por classe de teste.
/// </summary>
[CollectionDefinition(Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit ICollectionFixture<T> requires the collection definition type to be public.")]
[SuppressMessage(
    "Naming",
    "CA1711:Identifiers should not have incorrect suffix",
    Justification = "Convenção de nome de coleção xUnit termina com 'Collection'.")]
public sealed class ConfiguracaoDbCollection : ICollectionFixture<ConfiguracaoDbFixture>
{
    public const string Name = "configuracao-db";
}
