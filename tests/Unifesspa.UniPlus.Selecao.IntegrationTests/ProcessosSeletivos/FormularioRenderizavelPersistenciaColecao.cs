namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

/// <summary>
/// Uma só instância do Postgres efêmero para as classes de teste do formulário público lido do banco:
/// cada teste publica o seu próprio processo, com nome e identificador legível distintos, e lê só o
/// que conhece pelo id.
/// </summary>
[CollectionDefinition(Nome)]
public sealed class FormularioRenderizavelPersistenciaColecao : ICollectionFixture<ProcessoSeletivoDbFixture>
{
    public const string Nome = "formulario-renderizavel-persistencia";
}
