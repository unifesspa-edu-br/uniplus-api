namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

public sealed class FormatosTextoTests
{
    [Fact(DisplayName = "Todo formato do catálogo é conferido pela avaliação do formulário")]
    public void TodoFormato_EhConferidoPelaAvaliacao()
    {
        foreach (FormatoTexto formato in CatalogoDe<FormatoTexto>.Valores())
        {
            Action conferir = () => _ = new FormatoDeTexto(formato.Codigo());

            conferir.Should().NotThrow($"o formato {formato} tem de ter conferência na avaliação");
        }
    }
}
