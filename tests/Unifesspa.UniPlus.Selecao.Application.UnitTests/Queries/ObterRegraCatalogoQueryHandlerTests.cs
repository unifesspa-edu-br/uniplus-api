namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.RolDeRegras;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class ObterRegraCatalogoQueryHandlerTests
{
    [Fact(DisplayName = "A regra ausente do catálogo é recusada sem citar o código nem a versão pedidos")]
    public async Task Handle_RegraAusente_RecusaSemCitarCodigoNemVersao()
    {
        IRegraCatalogoReader reader = Substitute.For<IRegraCatalogoReader>();
        reader.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns((RegraCatalogo?)null);

        Result<RegraCatalogoDto> resultado = await ObterRegraCatalogoQueryHandler.Handle(
            new ObterRegraCatalogoQuery("REGRA_INEXISTENTE_DE_TESTE", "versao-inexistente-de-teste"), reader, CancellationToken.None);

        resultado.Error!.Code.Should().Be("RegraCatalogo.NaoEncontrada");
        resultado.Error.Message.Should().NotContain("REGRA_INEXISTENTE_DE_TESTE").And.NotContain("versao-inexistente-de-teste");
    }
}
