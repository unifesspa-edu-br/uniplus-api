namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

public sealed class AcessoAoProcessoNaUnidadeAtualTests
{
    private readonly IVerificadorDeAcesso _verificador = Substitute.For<IVerificadorDeAcesso>();
    private readonly Guid _unidadeId = Guid.CreateVersion7();
    private readonly Guid _processoId = Guid.CreateVersion7();

    private Task<Result> Exigir() =>
        AcessoAoProcesso.ExigirConfigurarNaUnidadeAtualAsync(_verificador, _unidadeId, _processoId, CancellationToken.None);

    private void Decide(ResultadoDoAcesso resultado) =>
        _verificador.ExigirAsync(default!, default, default).ReturnsForAnyArgs(resultado);

    [Fact(DisplayName = "Permitido na unidade atual segue e decide sobre o processo nessa unidade")]
    public async Task Permitido_Segue()
    {
        Decide(ResultadoDoAcesso.Permitido);

        Result resultado = await Exigir();

        resultado.IsSuccess.Should().BeTrue();
        await _verificador.Received(1).ExigirAsync(
            Arg.Any<PermissionRequirement>(),
            Arg.Is<ResourceContext?>(resource => resource!.UnidadeProprietariaId == _unidadeId && resource.ProcessoId == _processoId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Negado na unidade atual recusa com o 404 do processo que não existe")]
    public async Task Negado_NaoEncontrado()
    {
        Decide(ResultadoDoAcesso.Negado);

        Result resultado = await Exigir();

        resultado.Error.Should().BeEquivalentTo(AcessoAoProcesso.NaoEncontrado());
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "A identidade incompleta recusa como o filtro: pede nova autenticação")]
    public async Task IdentidadeIncompleta_RecusaDeIdentidade()
    {
        Decide(ResultadoDoAcesso.IdentidadeIncompleta);

        Result resultado = await Exigir();

        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.IdentidadeIncompleta);
    }

    [Fact(DisplayName = "Quem tem a permissão e está inscrito no processo recusa como o filtro: impedido")]
    public async Task Impedido_RecusaDeImpedimento()
    {
        Decide(ResultadoDoAcesso.Impedido);

        Result resultado = await Exigir();

        resultado.Error!.Code.Should().Be(ProcessoSeletivoErrorCodes.ImpedidoPorInscricaoNoProcesso);
    }
}
