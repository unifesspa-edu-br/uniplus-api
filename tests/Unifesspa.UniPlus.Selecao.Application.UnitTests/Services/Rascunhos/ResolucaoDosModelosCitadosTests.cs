namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.Extensions.Time.Testing;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

public sealed class ResolucaoDosModelosCitadosTests
{
    private readonly FakeTimeProvider _relogio = new();
    private readonly IModeloDeDocumentoRepository _modelos = Substitute.For<IModeloDeDocumentoRepository>();

    [Fact]
    public async Task Resolver_CitacoesDeEstadosDistintos_AcumulaCausasNaOrdemEConsultaUmaVez()
    {
        Guid dono = Guid.CreateVersion7();
        ModeloDeDocumento pendente = ModeloDeDocumento.IniciarPendente(dono, "Modelo", "ODT", _relogio, TimeSpan.FromMinutes(15)).Value!;
        Guid ausente = Guid.CreateVersion7();
        _modelos.ListarDoProcessoAsync(dono, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([pendente]);
        JsonObject documento = Documento(ausente, pendente.Id, ausente);
        ResultadoDosModelosCitados resultado = await new ResolucaoDosModelosCitados(_modelos).ResolverAsync(dono, documento, CancellationToken.None);
        resultado.Causas.Select(causa => causa.Field).Should().Equal(
            "/documentosExigidos/exigencias/0/modeloId", "/documentosExigidos/exigencias/1/modeloId", "/documentosExigidos/exigencias/2/modeloId");
        resultado.Causas.Select(causa => causa.Error.Code).Should().Equal(
            ModeloDaExigenciaErrorCodes.ModeloNaoEncontrado, ModeloDeDocumentoErrorCodes.NaoConfirmado, ModeloDaExigenciaErrorCodes.ModeloNaoEncontrado);
        await _modelos.Received(1).ListarDoProcessoAsync(dono, Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolver_CitacoesResolvidasERecusadas_PreservaAsPosicoesOriginais()
    {
        Guid dono = Guid.CreateVersion7();
        ModeloDeDocumento confirmado = ModeloDeDocumento.IniciarPendente(dono, "Modelo", "ODT", _relogio, TimeSpan.FromMinutes(15)).Value!;
        confirmado.Confirmar(1024, new string('a', 64), _relogio).IsSuccess.Should().BeTrue();
        ModeloDeDocumento pendente = ModeloDeDocumento.IniciarPendente(dono, "Pendente", "ODT", _relogio, TimeSpan.FromMinutes(15)).Value!;
        _modelos.ListarDoProcessoAsync(dono, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([confirmado, pendente]);
        JsonObject documento = Documento(Guid.CreateVersion7(), confirmado.Id, pendente.Id, confirmado.Id);
        ResultadoDosModelosCitados resultado = await new ResolucaoDosModelosCitados(_modelos).ResolverAsync(dono, documento, CancellationToken.None);

        resultado.Modelos.Keys.Should().Equal(1, 3);
        resultado.Modelos.Values.Select(modelo => modelo.ModeloId).Should().Equal(confirmado.Id, confirmado.Id);
        resultado.Causas.Select(causa => causa.Field).Should().Equal(
            "/documentosExigidos/exigencias/0/modeloId", "/documentosExigidos/exigencias/2/modeloId");
        resultado.Causas.Select(causa => causa.Error.Code).Should().Equal(
            ModeloDaExigenciaErrorCodes.ModeloNaoEncontrado, ModeloDeDocumentoErrorCodes.NaoConfirmado);
    }

    [Fact]
    public async Task Resolver_SemCitacoes_NaoConsultaModelos()
    {
        ResultadoDosModelosCitados resultado = await new ResolucaoDosModelosCitados(_modelos).ResolverAsync(Guid.CreateVersion7(), new JsonObject(), CancellationToken.None);
        resultado.Causas.Should().BeEmpty();
        resultado.Modelos.Should().BeEmpty();
        await _modelos.DidNotReceiveWithAnyArgs().ListarDoProcessoAsync(default, default!, default);
    }

    private static JsonObject Documento(params Guid[] identificadores) => new()
    {
        ["documentosExigidos"] = new JsonObject
        {
            ["exigencias"] = new JsonArray(identificadores.Select(id => (JsonNode)new JsonObject { ["modeloId"] = id.ToString() }).ToArray()),
        },
    };
}
