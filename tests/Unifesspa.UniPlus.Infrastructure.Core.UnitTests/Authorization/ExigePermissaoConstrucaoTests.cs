namespace Unifesspa.UniPlus.Infrastructure.Core.UnitTests.Authorization;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Infrastructure.Core.Authorization;

public sealed class ExigePermissaoConstrucaoTests
{
    [Fact]
    public void Construtor_ValorIndefinido_RecusaPermissao()
    {
        Func<ExigePermissaoAttribute<PermissaoAutorizacao>> criar = () => new((PermissaoAutorizacao)999);

        criar.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*999*PermissaoAutorizacao*");
    }
}
