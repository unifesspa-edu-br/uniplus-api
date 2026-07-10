namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

using System.Diagnostics.CodeAnalysis;

/// <summary>
/// Coleção que compartilha um único <see cref="PublicacoesDbFixture"/> (Postgres
/// efêmero) entre as suítes de persistência do módulo — evita subir um container
/// por classe de teste.
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
public sealed class PublicacoesDbCollection : ICollectionFixture<PublicacoesDbFixture>
{
    public const string Name = "publicacoes-db";
}
