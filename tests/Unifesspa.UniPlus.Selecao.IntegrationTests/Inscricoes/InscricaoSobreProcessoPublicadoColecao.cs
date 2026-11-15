namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Inscricoes;

/// <summary>
/// Uma só instância do Postgres efêmero para todas as classes de teste da inscrição sobre o processo
/// publicado: cada teste publica o seu próprio processo, com nome e identificador legível distintos.
/// </summary>
[CollectionDefinition(Nome)]
public sealed class InscricaoSobreProcessoPublicadoColecao : ICollectionFixture<InscricaoDbFixture>
{
    public const string Nome = "inscricao-sobre-processo-publicado";
}
