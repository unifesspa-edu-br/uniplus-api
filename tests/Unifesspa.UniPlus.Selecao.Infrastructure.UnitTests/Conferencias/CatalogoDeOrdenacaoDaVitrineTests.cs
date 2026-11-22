namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Conferencias;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories.Ordenacao;

public sealed class CatalogoDeOrdenacaoDaVitrineTests
{
    [Fact(DisplayName = "O catálogo e o mapeamento de ordenação coincidem")]
    public void Catalogo_ContratoEMapeamento_Coincidem() =>
        OrdenacaoDaVitrine.CamposMapeados.Should().BeEquivalentTo(CamposOrdenacaoDaVitrine.Todos);
}
