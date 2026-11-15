namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

/// <summary>
/// Uma só instância do Postgres efêmero para as classes de teste do snapshot publicado e da cascata
/// de remanejamento persistida: cada teste cria o seu próprio processo, com nome e identificador
/// legível distintos, e filtra o que lê pelos ids que conhece.
/// </summary>
[CollectionDefinition(Nome)]
public sealed class PublicacaoSnapshotPersistenciaColecao : ICollectionFixture<ProcessoSeletivoDbFixture>
{
    public const string Nome = "publicacao-snapshot-persistencia";
}
