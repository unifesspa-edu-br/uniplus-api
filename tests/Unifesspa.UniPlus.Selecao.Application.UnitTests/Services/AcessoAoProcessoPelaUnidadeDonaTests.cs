namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class AcessoAoProcessoPelaUnidadeDonaTests
{
    private static readonly Guid UnidadeDona = Guid.CreateVersion7();

    private readonly IVerificadorDeAcesso _verificador = Substitute.For<IVerificadorDeAcesso>();
    private readonly IUnidadeDonaDoProcesso _unidades = Substitute.For<IUnidadeDonaDoProcesso>();

    [Fact(DisplayName = "Com unidade dona, a decisão é pedida ao ponto de decisão sobre o resource da unidade e do processo")]
    public async Task ComUnidadeDona_DecidePeloResourceDaUnidadeEDoProcesso()
    {
        Guid processoId = Guid.CreateVersion7();
        _unidades.DoProcessoAsync(processoId, Arg.Any<CancellationToken>()).Returns(UnidadeDona);
        _verificador.ExigirAsync(default!, default, default).ReturnsForAnyArgs(ResultadoDoAcesso.Permitido);

        ResultadoDoAcesso resultado = await Verificar(processoId);

        resultado.Should().Be(ResultadoDoAcesso.Permitido);
        await _verificador.Received(1).ExigirAsync(
            PermissaoSelecao.ProcessosConfigurar.Requisito,
            Arg.Is<ResourceContext?>(r => r!.UnidadeProprietariaId == UnidadeDona && r.ProcessoId == processoId),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A recusa do ponto de decisão sobre a unidade dona é devolvida como está")]
    public async Task ComUnidadeDona_DevolveARecusaDoPontoDeDecisao()
    {
        Guid processoId = Guid.CreateVersion7();
        _unidades.DoProcessoAsync(processoId, Arg.Any<CancellationToken>()).Returns(UnidadeDona);
        _verificador.ExigirAsync(default!, default, default).ReturnsForAnyArgs(ResultadoDoAcesso.Impedido);

        (await Verificar(processoId)).Should().Be(ResultadoDoAcesso.Impedido);
    }

    [Fact(DisplayName = "Processo e rascunho inexistentes são negados sem consultar o ponto de decisão, que não registra decisão sobre o que não existe")]
    public async Task SemUnidadeDona_NegaSemConsultarOVerificador()
    {
        _unidades.DoProcessoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Guid?)null);

        ResultadoDoAcesso resultado = await Verificar(Guid.CreateVersion7());

        resultado.Should().Be(ResultadoDoAcesso.Negado);
        await _verificador.DidNotReceiveWithAnyArgs().ExigirAsync(default!, default, default);
    }

    [Fact(DisplayName = "A unidade dona é perguntada pelo identificador da rota")]
    public async Task PerguntaAUnidadePeloIdentificadorDaRota()
    {
        Guid processoId = Guid.CreateVersion7();

        await Verificar(processoId);

        await _unidades.Received(1).DoProcessoAsync(processoId, Arg.Any<CancellationToken>());
    }

    private Task<ResultadoDoAcesso> Verificar(Guid processoId) =>
        AcessoAoProcesso.VerificarAsync(
            _verificador, _unidades, processoId, PermissaoSelecao.ProcessosConfigurar,
            unidade => AcessoAoProcesso.ResourceDoProcesso(unidade, processoId), CancellationToken.None);
}
