namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Services;

public sealed class AcessoAoProcessoUnidadesTests
{
    [Fact]
    public async Task UnidadesOndeConfigura_IgnoraConcessaoSemUnidadeENaoRepete()
    {
        Guid unidade = Guid.CreateVersion7();
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.PodeAsync(default!, default, default).ReturnsForAnyArgs(ResultadoDoAcesso.Permitido);
        Guid[] resultado = await AcessoAoProcesso.UnidadesOndeConfiguraAsync(
            verificador, [Concessao(null), Concessao(unidade), Concessao(unidade)], CancellationToken.None);
        resultado.Should().Equal(unidade);
        await verificador.ReceivedWithAnyArgs(1).PodeAsync(default!, default, default);
    }

    [Fact]
    public async Task UnidadesOndeConfigura_RetornaSomenteUnidadesPermitidas()
    {
        Guid permitida = Guid.CreateVersion7();
        Guid negada = Guid.CreateVersion7();
        IVerificadorDeAcesso verificador = Substitute.For<IVerificadorDeAcesso>();
        verificador.PodeAsync(default!, default, default).ReturnsForAnyArgs(ResultadoDoAcesso.Negado);
        verificador.PodeAsync(Arg.Any<PermissionRequirement>(), Arg.Is<ResourceContext?>(r => r!.UnidadeProprietariaId == permitida), Arg.Any<CancellationToken>())
            .Returns(ResultadoDoAcesso.Permitido);
        Guid[] resultado = await AcessoAoProcesso.UnidadesOndeConfiguraAsync(
            verificador, [Concessao(permitida), Concessao(negada)], CancellationToken.None);
        resultado.Should().Equal(permitida);
    }

    private static ConcessaoAtiva Concessao(Guid? unidade) =>
        new(Guid.CreateVersion7(), Guid.CreateVersion7(), null, "Perfil", unidade, unidade is null, null, []);
}
