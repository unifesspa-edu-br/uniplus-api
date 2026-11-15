namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

/// <summary>
/// Uma só instância do Postgres efêmero para as classes de teste da classificação lida do banco:
/// cada teste persiste o seu próprio processo, com identificadores distintos, e lê só o que conhece
/// pelo id.
/// </summary>
[CollectionDefinition(Nome)]
public sealed class ClassificacaoPersistenciaColecao : ICollectionFixture<ProcessoSeletivoDbFixture>
{
    public const string Nome = "classificacao-persistencia";
}
