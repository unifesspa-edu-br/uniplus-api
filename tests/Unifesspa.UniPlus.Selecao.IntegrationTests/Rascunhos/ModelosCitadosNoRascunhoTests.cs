namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static ClienteDoRascunhoDeTeste;

[Collection(CascadingCollection.Name)]
public sealed class ModelosCitadosNoRascunhoTests(CascadingFixture fixture)
{
    private const string Conta = "pessoa.ficticia";
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly ClienteDoRascunhoDeTeste _cliente = new(fixture);

    [Fact]
    public async Task Salvar_ModeloConfirmado_ExportaDescricaoERetiraCitacaoNoPut()
    {
        Guid rascunho = await CriarAsync();
        Guid modelo = await GravarModeloAsync(rascunho, confirmado: true);
        using HttpResponseMessage salvo = await SalvarAsync(rascunho, DocumentoComModelo(modelo), "1");
        (await Corpo(salvo)).GetProperty("diagnosticos").GetArrayLength().Should().Be(0);
        await ConferirExportacaoAsync(rascunho, modelo);
        using HttpResponseMessage retirado = await SalvarAsync(rascunho, DocumentoSemModelo(), "2");
        retirado.StatusCode.Should().Be(HttpStatusCode.OK);
        using HttpResponseMessage leitura = await _cliente.EnviarAsync(Conta, HttpMethod.Get, $"{Rota}/{rascunho}");
        JsonElement exigencia = (await Corpo(leitura)).GetProperty("documentosExigidos").GetProperty("exigencias")[0];
        exigencia.TryGetProperty("modeloId", out _).Should().BeFalse();
        exigencia.TryGetProperty("modelo", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Salvar_ModeloInexistente_DevolveDiagnosticoSemIdentificador()
    {
        Guid rascunho = await CriarAsync();
        Guid modelo = Guid.CreateVersion7();
        using HttpResponseMessage salvo = await SalvarAsync(rascunho, DocumentoComModelo(modelo), "1");
        await ConferirDiagnosticoAsync(salvo, "uniplus.selecao.documento_exigido.modelo_nao_encontrado", modelo);
    }

    [Fact]
    public async Task Salvar_ModeloPendente_DevolveDiagnostico()
    {
        Guid rascunho = await CriarAsync();
        Guid modelo = await GravarModeloAsync(rascunho, confirmado: false);
        using HttpResponseMessage salvo = await SalvarAsync(rascunho, DocumentoComModelo(modelo), "1");
        await ConferirDiagnosticoAsync(salvo, "uniplus.selecao.modelo_de_documento.nao_confirmado", modelo);
        using HttpResponseMessage leitura = await _cliente.EnviarAsync(Conta, HttpMethod.Get, $"{Rota}/{rascunho}");
        JsonElement exigencia = (await Corpo(leitura)).GetProperty("documentosExigidos").GetProperty("exigencias")[0];
        exigencia.TryGetProperty("modelo", out _).Should().BeFalse();
    }

    private async Task<Guid> CriarAsync()
    {
        await _cliente.ConcederAsync(Conta, UnidadesDeTeste.Ceps.OrigemId);
        return await _cliente.CriarAsync(Conta, "CEPS");
    }

    private async Task<Guid> GravarModeloAsync(Guid dono, bool confirmado)
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ModeloDeDocumento modelo = ModeloDeDocumento.IniciarPendente(dono, "Autodeclaração", "ODT", _relogio, EnviosPendentesDeTeste.PrazoDeEnvio).Value!;
        if (confirmado)
        {
            modelo.Confirmar(1, new string('a', 64), _relogio).IsSuccess.Should().BeTrue();
        }
        banco.ModelosDeDocumento.Add(modelo);
        await banco.SaveChangesAsync();
        return modelo.Id;
    }

    private async Task ConferirExportacaoAsync(Guid rascunho, Guid modelo)
    {
        using HttpResponseMessage resposta = await _cliente.EnviarAsync(Conta, HttpMethod.Get, $"{Rota}/{rascunho}");
        JsonElement exigencia = (await Corpo(resposta)).GetProperty("documentosExigidos").GetProperty("exigencias")[0];
        exigencia.GetProperty("modeloId").GetGuid().Should().Be(modelo);
        JsonElement descricao = exigencia.GetProperty("modelo");
        descricao.GetProperty("nomeArquivo").GetString().Should().Be("Autodeclaração.odt");
        descricao.GetProperty("formato").GetString().Should().Be("ODT");
        descricao.GetProperty("hashSha256").GetString().Should().Be(new string('a', 64));
    }

    private static async Task ConferirDiagnosticoAsync(HttpResponseMessage resposta, string codigo, Guid modelo)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement causa = (await Corpo(resposta)).GetProperty("diagnosticos")[0];
        causa.GetProperty("code").GetString().Should().Be(codigo);
        causa.GetProperty("field").GetString().Should().Be("/documentosExigidos/exigencias/0/modeloId");
        causa.GetProperty("message").GetString().Should().NotContain(modelo.ToString());
    }

    private Task<HttpResponseMessage> SalvarAsync(Guid rascunho, string documento, string revisao) =>
        _cliente.EnviarAsync(Conta, HttpMethod.Put, $"{Rota}/{rascunho}", documento, ifMatch: $"\"{revisao}\"");

    private static string DocumentoSemModelo() => """
        { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
          "identificacao": { "nome": "Processo de exemplo", "unidadeAdministradoraCodigo": "CEPS" },
          "documentosExigidos": { "exigencias": [ { "chave": "declaracao" } ] } }
        """;

    private static string DocumentoComModelo(Guid modelo) => $$"""
        { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
          "identificacao": { "nome": "Processo de exemplo", "unidadeAdministradoraCodigo": "CEPS" },
          "documentosExigidos": { "exigencias": [ { "chave": "declaracao", "modeloId": "{{modelo}}" } ] } }
        """;
}
