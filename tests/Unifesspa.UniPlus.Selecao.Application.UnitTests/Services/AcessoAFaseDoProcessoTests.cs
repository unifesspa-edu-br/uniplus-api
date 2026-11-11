namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class AcessoAFaseDoProcessoTests
{
    private readonly IVerificadorDeAcesso _verificador = Substitute.For<IVerificadorDeAcesso>();
    private readonly IUnidadeDaFaseNaVersaoVigente _fases = Substitute.For<IUnidadeDaFaseNaVersaoVigente>();
    private readonly Guid _processoId = Guid.CreateVersion7();
    private readonly UnidadeDaFase _fase = new(Guid.CreateVersion7(), Guid.CreateVersion7());

    private static PermissaoSelecao Permissao => PermissaoSelecao.DocumentosDaFasePublicar;

    [Fact(DisplayName = "A ação da fase decide na unidade da fase, com o processo e a fase no resource")]
    public async Task ExigirNaFase_DecideNaUnidadeDaFase()
    {
        _fases.DaFaseAsync(_processoId, _fase.FaseId, Arg.Any<CancellationToken>()).Returns(_fase);
        _verificador.ExigirAsync(Permissao.Requisito, Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoDoAcesso.Permitido);

        ResultadoDoAcesso resultado = await AcessoAoProcesso.ExigirNaFaseAsync(
            _verificador, _fases, _processoId, _fase.FaseId, Permissao, CancellationToken.None);

        resultado.Should().Be(ResultadoDoAcesso.Permitido);
        ResourceContext resource = (ResourceContext)_verificador.ReceivedCalls().Single().GetArguments()[1]!;
        resource.UnidadeProprietariaId.Should().Be(_fase.UnidadeId);
        resource.ProcessoId.Should().Be(_processoId);
        resource.ResourceId.Should().Be(_fase.FaseId);
        resource.ResourceTipo.Should().Be(ResourceTiposSelecao.FaseDoProcesso);
    }

    [Fact(DisplayName = "A negativa do ponto de decisão na unidade da fase é devolvida como está")]
    public async Task ExigirNaFase_NegativaDoVerificador_Propaga()
    {
        _fases.DaFaseAsync(_processoId, _fase.FaseId, Arg.Any<CancellationToken>()).Returns(_fase);
        _verificador.ExigirAsync(Permissao.Requisito, Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoDoAcesso.Negado);

        ResultadoDoAcesso resultado = await AcessoAoProcesso.ExigirNaFaseAsync(
            _verificador, _fases, _processoId, _fase.FaseId, Permissao, CancellationToken.None);

        resultado.Should().Be(ResultadoDoAcesso.Negado);
    }

    [Fact(DisplayName = "O impedimento por inscrição na unidade da fase é devolvido como está")]
    public async Task ExigirNaFase_Impedido_Propaga()
    {
        _fases.DaFaseAsync(_processoId, _fase.FaseId, Arg.Any<CancellationToken>()).Returns(_fase);
        _verificador.ExigirAsync(Permissao.Requisito, Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoDoAcesso.Impedido);

        ResultadoDoAcesso resultado = await AcessoAoProcesso.ExigirNaFaseAsync(
            _verificador, _fases, _processoId, _fase.FaseId, Permissao, CancellationToken.None);

        resultado.Should().Be(ResultadoDoAcesso.Impedido);
    }

    [Fact(DisplayName = "A fase que não se resolve é negada sem consultar o ponto de decisão")]
    public async Task ExigirNaFase_FaseNaoResolvida_NegaSemConsultarOVerificador()
    {
        _fases.DaFaseAsync(_processoId, _fase.FaseId, Arg.Any<CancellationToken>()).Returns((UnidadeDaFase?)null);

        ResultadoDoAcesso resultado = await AcessoAoProcesso.ExigirNaFaseAsync(
            _verificador, _fases, _processoId, _fase.FaseId, Permissao, CancellationToken.None);

        resultado.Should().Be(ResultadoDoAcesso.Negado);
        await _verificador.DidNotReceiveWithAnyArgs().ExigirAsync(default!, default, default);
    }

    [Fact(DisplayName = "A ação de uma etapa decide na unidade da fase que a contém")]
    public async Task ExigirNaEtapa_DecideNaUnidadeDaFaseDaEtapa()
    {
        Guid etapaId = Guid.CreateVersion7();
        _fases.DaEtapaAsync(_processoId, etapaId, Arg.Any<CancellationToken>()).Returns(_fase);
        _verificador.ExigirAsync(Permissao.Requisito, Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoDoAcesso.Permitido);

        ResultadoDoAcesso resultado = await AcessoAoProcesso.ExigirNaEtapaAsync(
            _verificador, _fases, _processoId, etapaId, Permissao, CancellationToken.None);

        resultado.Should().Be(ResultadoDoAcesso.Permitido);
        ResourceContext resource = (ResourceContext)_verificador.ReceivedCalls().Single().GetArguments()[1]!;
        resource.UnidadeProprietariaId.Should().Be(_fase.UnidadeId);
        resource.ProcessoId.Should().Be(_processoId);
    }

    [Fact(DisplayName = "A etapa sem fase resolvida é negada sem consultar o ponto de decisão")]
    public async Task ExigirNaEtapa_SemFaseResolvida_NegaSemConsultarOVerificador()
    {
        _fases.DaEtapaAsync(_processoId, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((UnidadeDaFase?)null);

        ResultadoDoAcesso resultado = await AcessoAoProcesso.ExigirNaEtapaAsync(
            _verificador, _fases, _processoId, Guid.CreateVersion7(), Permissao, CancellationToken.None);

        resultado.Should().Be(ResultadoDoAcesso.Negado);
        await _verificador.DidNotReceiveWithAnyArgs().ExigirAsync(default!, default, default);
    }

    [Fact(DisplayName = "A consulta de capacidade da fase usa a unidade da fase e não registra decisão")]
    public async Task PodeNaFase_ConsultaCapacidadeSemRegistrar()
    {
        _fases.DaFaseAsync(_processoId, _fase.FaseId, Arg.Any<CancellationToken>()).Returns(_fase);
        _verificador.PodeAsync(Permissao.Requisito, Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>())
            .Returns(ResultadoDoAcesso.Permitido);

        ResultadoDoAcesso resultado = await AcessoAoProcesso.PodeNaFaseAsync(
            _verificador, _fases, _processoId, _fase.FaseId, Permissao, CancellationToken.None);

        resultado.Should().Be(ResultadoDoAcesso.Permitido);
        await _verificador.DidNotReceiveWithAnyArgs().ExigirAsync(default!, default, default);
    }

    [Fact(DisplayName = "A consulta de capacidade de fase que não se resolve é negada sem consultar o ponto de decisão")]
    public async Task PodeNaFase_FaseNaoResolvida_Nega()
    {
        _fases.DaFaseAsync(_processoId, _fase.FaseId, Arg.Any<CancellationToken>()).Returns((UnidadeDaFase?)null);

        ResultadoDoAcesso resultado = await AcessoAoProcesso.PodeNaFaseAsync(
            _verificador, _fases, _processoId, _fase.FaseId, Permissao, CancellationToken.None);

        resultado.Should().Be(ResultadoDoAcesso.Negado);
        await _verificador.DidNotReceiveWithAnyArgs().PodeAsync(default!, default, default);
    }
}
