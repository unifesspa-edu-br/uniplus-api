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
public sealed class ImportacaoDoModeloDeOutroRascunhoTests(CascadingFixture fixture)
{
    private const string Conta = "pessoa.ficticia";
    private readonly FakeTimeProvider _relogio = new(new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero));
    private readonly ClienteDoRascunhoDeTeste _cliente = new(fixture);

    [Fact]
    public async Task Importar_ModeloDeOutroRascunho_ApontaModeloNaoEncontrado()
    {
        await _cliente.ConcederAsync(Conta, UnidadesDeTeste.Ceps.OrigemId);
        Guid origem = await _cliente.CriarAsync(Conta, "CEPS");
        Guid destino = await _cliente.CriarAsync(Conta, "CEPS");
        Guid modelo = await GravarModeloAsync(origem);
        await SalvarNaOrigemAsync(origem, modelo);
        await ImportarEConferirAsync(origem, destino);
    }

    private async Task ImportarEConferirAsync(Guid origem, Guid destino)
    {
        using HttpResponseMessage exportado = await _cliente.EnviarAsync(Conta, HttpMethod.Get, $"{Rota}/{origem}");
        exportado.StatusCode.Should().Be(HttpStatusCode.OK);
        using HttpResponseMessage importado = await _cliente.EnviarAsync(Conta, HttpMethod.Put, $"{Rota}/{destino}",
            await exportado.Content.ReadAsStringAsync(), ifMatch: "\"1\"");
        importado.StatusCode.Should().Be(HttpStatusCode.OK);
        JsonElement diagnosticos = (await Corpo(importado)).GetProperty("diagnosticos");
        diagnosticos.EnumerateArray().Should().Contain(item =>
            item.GetProperty("code").GetString() == "uniplus.selecao.documento_exigido.modelo_nao_encontrado"
            && item.GetProperty("field").GetString() == "/documentosExigidos/exigencias/0/modeloId");
    }

    private async Task<Guid> GravarModeloAsync(Guid origem)
    {
        await using AsyncServiceScope escopo = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext banco = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ModeloDeDocumento modelo = ModeloDeDocumento.IniciarPendente(origem, "Autodeclaração", "ODT", _relogio, EnviosPendentesDeTeste.PrazoDeEnvio).Value!;
        modelo.Confirmar(1, new string('a', 64), _relogio).IsSuccess.Should().BeTrue();
        banco.ModelosDeDocumento.Add(modelo);
        await banco.SaveChangesAsync();
        return modelo.Id;
    }

    private async Task SalvarNaOrigemAsync(Guid origem, Guid modelo)
    {
        string documento = $$"""
            { "formato": "uniplus.selecao.configuracao", "versaoFormato": 1,
              "identificacao": { "nome": "Processo de exemplo", "unidadeAdministradoraCodigo": "CEPS" },
              "documentosExigidos": { "exigencias": [ { "chave": "declaracao", "modeloId": "{{modelo}}" } ] } }
            """;
        using HttpResponseMessage salvo = await _cliente.EnviarAsync(Conta, HttpMethod.Put, $"{Rota}/{origem}", documento, ifMatch: "\"1\"");
        salvo.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
