namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

/// <summary>
/// Uma só instância do Postgres efêmero para todas as classes de teste da persistência da retificação:
/// cada teste publica o seu próprio processo, com nome e identificador legível distintos.
/// </summary>
[CollectionDefinition(Nome)]
public sealed class RetificacaoPersistenciaColecao : ICollectionFixture<ProcessoSeletivoDbFixture>
{
    public const string Nome = "retificacao-persistencia";
}
