namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

/// <summary>
/// Uma só instância do Postgres efêmero para as classes de teste do seletor da versão vigente:
/// cada teste publica o seu próprio processo, com nome e identificador legível distintos, e lê só o
/// que conhece pelo id.
/// </summary>
[CollectionDefinition(Nome)]
public sealed class VersaoVigentePersistenciaColecao : ICollectionFixture<ProcessoSeletivoDbFixture>
{
    public const string Nome = "versao-vigente-persistencia";
}
