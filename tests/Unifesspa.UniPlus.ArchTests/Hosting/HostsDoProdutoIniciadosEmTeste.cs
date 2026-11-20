namespace Unifesspa.UniPlus.ArchTests.Hosting;

/// <summary>
/// Os hosts do Host e do Portal, iniciados uma só vez por execução e compartilhados pelas classes de teste da coleção:
/// cada início custa a composição completa de um processo, e duas coleções os iniciariam duas vezes, ao mesmo tempo.
/// </summary>
[CollectionDefinition(Nome)]
public sealed class HostsDoProdutoIniciadosEmTeste : ICollectionFixture<MigrationOrderFixture>
{
    public const string Nome = "Hosts do produto iniciados em teste";
}
