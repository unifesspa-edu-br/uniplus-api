namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Persistence;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;

public sealed class PrepararTermoDeBuscaTests
{
    [Fact]
    public void PrepararTermoDeBusca_TermoValidadoAusente_DevolveNulo() =>
        NormalizacaoTextual.PrepararTermoDeBusca((TermoDeBusca?)null).Should().BeNull();

    [Fact]
    public void PrepararTermoDeBusca_TermoValidadoComCuringas_EscapaTextoNormalizado()
    {
        TermoDeBusca? termo = TermoDeBusca.Criar("  Ação%_\\  ").Value;

        NormalizacaoTextual.PrepararTermoDeBusca(termo).Should().Be("acao\\%\\_\\\\");
    }
}
