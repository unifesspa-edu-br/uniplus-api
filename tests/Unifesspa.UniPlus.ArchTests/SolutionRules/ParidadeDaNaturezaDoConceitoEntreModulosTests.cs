namespace Unifesspa.UniPlus.ArchTests.SolutionRules;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using NaturezaDaConfiguracao = Unifesspa.UniPlus.Configuracao.Domain.Enums.NaturezaConceito;
using NaturezaDaSelecao = Unifesspa.UniPlus.Selecao.Domain.Enums.NaturezaConceito;

/// <summary>
/// A natureza do conceito existe nos dois módulos, que não se referenciam: o catálogo de tipos de conceito da
/// Configuração publica o código e a Seleção o lê com <c>CatalogoDe.Obter</c>. Códigos que divergem fariam a
/// leitura falhar em execução.
/// </summary>
public sealed class ParidadeDaNaturezaDoConceitoEntreModulosTests
{
    [Fact(DisplayName = "Os enums de natureza do conceito da Configuração e da Seleção declaram os mesmos códigos, na mesma ordem")]
    public void NaturezaDoConceito_ConfiguracaoESelecao_DeclaramOsMesmosCodigos() =>
        CatalogoDe<NaturezaDaSelecao>.Todos().Select(static d => d.Codigo).Should().Equal(
            CatalogoDe<NaturezaDaConfiguracao>.Todos().Select(static d => d.Codigo));
}
