namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A leitura do processo: o publicado é aberto a qualquer pessoa, e o rascunho só a quem configura na unidade dona
/// (ADR-0078, ADR-0151). Quem não pode ler o rascunho recebe o mesmo 404 de processo inexistente.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class LeituraDoProcessoEndpointTests : IAsyncLifetime
{
    private const string Rota = "/api/selecao/processos-seletivos";
    private const string Betina = "betina.neves";
    private const string Leandro = "leandro.viana";
    private const string Diogo = "diogo.souza";
    private const string LocalidadeNaoDivulgada = "1505502";

    private readonly CascadingFixture _fixture;

    public LeituraDoProcessoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    public async Task InitializeAsync()
    {
        foreach (string sub in new[] { Betina, Leandro, Diogo })
        {
            await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, sub);
        }
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "O processo publicado é lido sem token, por id, no snapshot vigente e na listagem")]
    public async Task Publicado_LidoSemToken()
    {
        Guid processoId = await SemearPublicadoAsync(nameof(Publicado_LidoSemToken));
        await AguardarDivulgacaoAsync(processoId);

        using HttpResponseMessage porId = await EnviarAsync(null, HttpMethod.Get, $"{Rota}/{processoId}");
        using HttpResponseMessage snapshot = await EnviarAsync(null, HttpMethod.Get, $"{Rota}/{processoId}/snapshot-vigente");
        using HttpResponseMessage listagem = await EnviarAsync(null, HttpMethod.Get, $"{Rota}?limit=100");

        porId.StatusCode.Should().Be(HttpStatusCode.OK);
        snapshot.StatusCode.Should().Be(HttpStatusCode.OK);
        (await IdsAsync(listagem)).Should().Contain(processoId);
    }

    [Fact(DisplayName = "O rascunho é lido por quem configura na unidade dona; em outra unidade, sem token ou sem concessão responde 404")]
    public async Task Rascunho_SoParaQuemConfiguraNaUnidadeDona()
    {
        Guid processoId = await SemearRascunhoAsync(nameof(Rascunho_SoParaQuemConfiguraNaUnidadeDona));
        await ConcederConfigurarAsync(Betina, await UnidadeDoProcessoAsync(processoId));
        await ConcederConfigurarAsync(Leandro, Guid.CreateVersion7());

        using HttpResponseMessage dona = await EnviarAsync(Betina, HttpMethod.Get, $"{Rota}/{processoId}");
        using HttpResponseMessage outraUnidade = await EnviarAsync(Leandro, HttpMethod.Get, $"{Rota}/{processoId}");
        using HttpResponseMessage semToken = await EnviarAsync(null, HttpMethod.Get, $"{Rota}/{processoId}");
        using HttpResponseMessage semConcessao = await EnviarAsync(Diogo, HttpMethod.Get, $"{Rota}/{processoId}", "plataforma-admin");
        using HttpResponseMessage snapshotSemToken = await EnviarAsync(null, HttpMethod.Get, $"{Rota}/{processoId}/snapshot-vigente");

        dona.StatusCode.Should().Be(HttpStatusCode.OK);
        outraUnidade.StatusCode.Should().Be(HttpStatusCode.NotFound);
        semToken.StatusCode.Should().Be(HttpStatusCode.NotFound);
        semConcessao.StatusCode.Should().Be(HttpStatusCode.NotFound);
        snapshotSemToken.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Revogada a concessão de quem criou o rascunho, a requisição seguinte responde 404")]
    public async Task Rascunho_ConcessaoRevogada_Responde404()
    {
        Guid processoId = await SemearRascunhoAsync(nameof(Rascunho_ConcessaoRevogada_Responde404));
        await ConcederConfigurarAsync(Diogo, await UnidadeDoProcessoAsync(processoId));
        using HttpResponseMessage antes = await EnviarAsync(Diogo, HttpMethod.Get, $"{Rota}/{processoId}");

        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Diogo);
        using HttpResponseMessage depois = await EnviarAsync(Diogo, HttpMethod.Get, $"{Rota}/{processoId}");

        antes.StatusCode.Should().Be(HttpStatusCode.OK);
        depois.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "A listagem traz os rascunhos só das unidades em que a pessoa configura; o anônimo vê só os publicados")]
    public async Task Listagem_RascunhoSoNaUnidadeQueConfigura()
    {
        Guid rascunhoDela = await SemearRascunhoAsync(nameof(Listagem_RascunhoSoNaUnidadeQueConfigura) + " dela");
        Guid rascunhoDosOutros = await SemearRascunhoAsync(nameof(Listagem_RascunhoSoNaUnidadeQueConfigura) + " dos outros");
        Guid publicado = await SemearPublicadoAsync(nameof(Listagem_RascunhoSoNaUnidadeQueConfigura));
        await ConcederConfigurarAsync(Betina, await UnidadeDoProcessoAsync(rascunhoDela));

        List<Guid> anonimo = await IdsAsync(await EnviarAsync(null, HttpMethod.Get, $"{Rota}?limit=100"));
        List<Guid> betina = await IdsAsync(await EnviarAsync(Betina, HttpMethod.Get, $"{Rota}?limit=100"));

        anonimo.Should().Contain(publicado).And.NotContain([rascunhoDela, rascunhoDosOutros]);
        betina.Should().Contain([publicado, rascunhoDela]).And.NotContain(rascunhoDosOutros);
    }

    [Fact(DisplayName = "Sob retificação, quem não configura lê o publicado sem a configuração em edição; quem configura a lê inteira")]
    public async Task SobRetificacao_ConfiguracaoEmEdicaoSoParaQuemConfigura()
    {
        Guid processoId = await SemearPublicadoAsync(nameof(SobRetificacao_ConfiguracaoEmEdicaoSoParaQuemConfigura));
        await ConcederConfigurarAsync(Betina, await UnidadeDoProcessoAsync(processoId));
        using HttpResponseMessage abertura = await EnviarAsync(
            null, HttpMethod.Post, $"{Rota}/{processoId}/retificacao-em-curso", "plataforma-admin", new { motivo = "Correção do peso" });
        abertura.StatusCode.Should().BeOneOf(HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NoContent);

        using HttpResponseMessage publico = await EnviarAsync(null, HttpMethod.Get, $"{Rota}/{processoId}");
        using HttpResponseMessage outraUnidade = await EnviarAsync(Leandro, HttpMethod.Get, $"{Rota}/{processoId}");
        using HttpResponseMessage quemConfigura = await EnviarAsync(Betina, HttpMethod.Get, $"{Rota}/{processoId}");

        (await EtapasAsync(publico)).Should().Be(0);
        (await EtapasAsync(outraUnidade)).Should().Be(0);
        (await EtapasAsync(quemConfigura)).Should().BeGreaterThan(0);
    }

    [Fact(DisplayName = "Mesmo sem retificação, quem não configura lê o publicado só com a identificação e a localidade publicada; quem configura lê tudo")]
    public async Task Publicado_ConfiguracaoAdministrativaSoParaQuemConfigura()
    {
        Guid processoId = await SemearPublicadoAsync(nameof(Publicado_ConfiguracaoAdministrativaSoParaQuemConfigura));
        await AguardarDivulgacaoAsync(processoId);
        await ConcederConfigurarAsync(Betina, await UnidadeDoProcessoAsync(processoId));

        using HttpResponseMessage anonimo = await EnviarAsync(null, HttpMethod.Get, $"{Rota}/{processoId}");
        using HttpResponseMessage quemConfigura = await EnviarAsync(Betina, HttpMethod.Get, $"{Rota}/{processoId}");

        (await EtapasAsync(anonimo)).Should().Be(0);
        (await EtapasAsync(quemConfigura)).Should().BeGreaterThan(0);
        (await CodigoIbgeAsync(anonimo)).Should().Be(await CodigoIbgeAsync(quemConfigura)).And.NotBeEmpty();
    }

    [Fact(DisplayName = "O snapshot vigente sai só com os blocos públicos para quem não configura; quem configura recebe o envelope inteiro")]
    public async Task SnapshotVigente_BlocosInternosSoParaQuemConfigura()
    {
        Guid processoId = await SemearPublicadoAsync(nameof(SnapshotVigente_BlocosInternosSoParaQuemConfigura));
        await AguardarDivulgacaoAsync(processoId);
        await ConcederConfigurarAsync(Betina, await UnidadeDoProcessoAsync(processoId));

        using HttpResponseMessage anonimo = await EnviarAsync(null, HttpMethod.Get, $"{Rota}/{processoId}/snapshot-vigente");
        using HttpResponseMessage quemConfigura = await EnviarAsync(Betina, HttpMethod.Get, $"{Rota}/{processoId}/snapshot-vigente");

        IReadOnlyList<string> doAnonimo = await BlocosAsync(anonimo);
        IReadOnlyList<string> deQuemConfigura = await BlocosAsync(quemConfigura);
        doAnonimo.Should().NotBeEmpty().And.OnlyContain(b => ClassificacaoDosBlocosDoCertame.Publicados.Contains(b));
        deQuemConfigura.Should().Contain(b => ClassificacaoDosBlocosDoCertame.Internos.Contains(b));
    }

    [Fact(DisplayName = "Com a retificação gravada e o ato ainda não divulgado, o snapshot vigente de quem não configura é o da versão divulgada; quem configura lê a nova")]
    public async Task SnapshotVigente_RetificacaoNaoDivulgada_NaoVazaParaQuemNaoConfigura()
    {
        Guid processoId = await SemearPublicadoAsync(nameof(SnapshotVigente_RetificacaoNaoDivulgada_NaoVazaParaQuemNaoConfigura));
        await ConcederConfigurarAsync(Betina, await UnidadeDoProcessoAsync(processoId));
        await AguardarDivulgacaoAsync(processoId);
        string hashDivulgado = await SemearVersaoNaoDivulgadaAsync(processoId);

        using HttpResponseMessage anonimo = await EnviarAsync(null, HttpMethod.Get, $"{Rota}/{processoId}/snapshot-vigente");
        using HttpResponseMessage quemConfigura = await EnviarAsync(Betina, HttpMethod.Get, $"{Rota}/{processoId}/snapshot-vigente");

        (await HashDaConfiguracaoAsync(anonimo)).Should().Be(hashDivulgado);
        (await HashDaConfiguracaoAsync(quemConfigura)).Should().NotBe(hashDivulgado);
    }

    [Fact(DisplayName = "Com a retificação gravada e o ato ainda não divulgado, a localidade de quem não configura é a da versão divulgada")]
    public async Task Processo_RetificacaoNaoDivulgada_LocalidadeDivulgadaParaQuemNaoConfigura()
    {
        Guid processoId = await SemearPublicadoAsync(nameof(Processo_RetificacaoNaoDivulgada_LocalidadeDivulgadaParaQuemNaoConfigura));
        await AguardarDivulgacaoAsync(processoId);
        using HttpResponseMessage antes = await EnviarAsync(null, HttpMethod.Get, $"{Rota}/{processoId}");
        string divulgada = await CodigoIbgeAsync(antes);
        divulgada.Should().NotBe(LocalidadeNaoDivulgada);
        await SemearVersaoNaoDivulgadaAsync(processoId);

        using HttpResponseMessage anonimo = await EnviarAsync(null, HttpMethod.Get, $"{Rota}/{processoId}");

        (await CodigoIbgeAsync(anonimo)).Should().Be(divulgada);
    }

    private static async Task<string> HashDaConfiguracaoAsync(HttpResponseMessage resposta)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        using JsonDocument json = JsonDocument.Parse(corpo);
        return json.RootElement.GetProperty("hashConfiguracao").GetString()!;
    }

    /// <summary>A divulgação nasce quando o ato é registrado, de forma assíncrona; a leitura pública depende dela.</summary>
    private async Task AguardarDivulgacaoAsync(Guid processoId)
    {
        for (int tentativa = 0; tentativa < 100; tentativa++)
        {
            await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
            if (await scope.ServiceProvider.GetRequiredService<SelecaoDbContext>().CertamesDivulgados.AsNoTracking()
                .AnyAsync(c => c.Id == processoId))
            {
                return;
            }

            await Task.Delay(300);
        }

        Assert.Fail($"O certame {processoId} não foi divulgado a tempo.");
    }

    /// <summary>Grava a versão seguinte sem avançar a divulgação, como a retificação cujo ato ainda não foi registrado. Devolve o hash da versão divulgada.</summary>
    private async Task<string> SemearVersaoNaoDivulgadaAsync(Guid processoId)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        VersaoConfiguracao v1 = await db.VersoesConfiguracao.AsNoTracking().SingleAsync(v => v.ProcessoSeletivoId == processoId);
        JsonObject configuracao = JsonNode.Parse(v1.ConfiguracaoCongelada)!.AsObject();
        configuracao["retificacaoNaoDivulgada"] = true;
        configuracao["localidade"] = new JsonObject { ["codigoIbge"] = LocalidadeNaoDivulgada, ["nome"] = "Marabá", ["uf"] = "PA" };

        db.VersoesConfiguracao.Add(VersaoConfiguracao.Suceder(
            v1,
            System.Text.Encoding.UTF8.GetBytes(configuracao.ToJsonString()),
            v1.SchemaVersion,
            v1.AlgoritmoHash,
            Guid.CreateVersion7(),
            v1.AtoCriadorHash,
            v1.AtoCriadorId,
            "plataforma-admin",
            DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return v1.HashConfiguracao;
    }

    private static async Task<IReadOnlyList<string>> BlocosAsync(HttpResponseMessage resposta)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        using JsonDocument json = JsonDocument.Parse(corpo);
        return [.. json.RootElement.GetProperty("configuracao").EnumerateObject().Select(static p => p.Name)];
    }

    [Fact(DisplayName = "A localidade alterada na retificação não aparece para quem não configura, que lê a publicada; quem configura lê a alterada")]
    public async Task SobRetificacao_LocalidadeEditadaNaoVazaParaQuemNaoConfigura()
    {
        Guid processoId = await SemearPublicadoAsync(nameof(SobRetificacao_LocalidadeEditadaNaoVazaParaQuemNaoConfigura));
        await AguardarDivulgacaoAsync(processoId);
        await ConcederConfigurarAsync(Betina, await UnidadeDoProcessoAsync(processoId));
        using HttpResponseMessage antes = await EnviarAsync(null, HttpMethod.Get, $"{Rota}/{processoId}");
        string publicada = await CodigoIbgeAsync(antes);
        const string Alterada = "1505502";
        publicada.Should().NotBe(Alterada);

        using HttpResponseMessage abertura = await EnviarAsync(
            null, HttpMethod.Post, $"{Rota}/{processoId}/retificacao-em-curso", "plataforma-admin", new { motivo = "Troca da localidade" });
        string etag = abertura.Headers.ETag!.ToString();
        using HttpResponseMessage edicao = await EnviarAsync(
            null, HttpMethod.Put, $"{Rota}/{processoId}/localidade", "plataforma-admin",
            new { codigoIbge = Alterada, nome = "Marabá", uf = "PA" }, etag);
        edicao.StatusCode.Should().Be(HttpStatusCode.NoContent, await edicao.Content.ReadAsStringAsync());

        using HttpResponseMessage anonimo = await EnviarAsync(null, HttpMethod.Get, $"{Rota}/{processoId}");
        using HttpResponseMessage quemConfigura = await EnviarAsync(Betina, HttpMethod.Get, $"{Rota}/{processoId}");

        (await CodigoIbgeAsync(anonimo)).Should().Be(publicada);
        (await CodigoIbgeAsync(quemConfigura)).Should().Be(Alterada);
    }

    private static async Task<string> CodigoIbgeAsync(HttpResponseMessage resposta)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        using JsonDocument json = JsonDocument.Parse(corpo);
        return json.RootElement.GetProperty("localidade").GetProperty("codigoIbge").GetString()!;
    }

    private static async Task<int> EtapasAsync(HttpResponseMessage resposta)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        using JsonDocument json = JsonDocument.Parse(corpo);
        return json.RootElement.GetProperty("etapas").GetArrayLength();
    }

    private static async Task<List<Guid>> IdsAsync(HttpResponseMessage resposta)
    {
        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, corpo);
        using JsonDocument json = JsonDocument.Parse(corpo);
        return [.. json.RootElement.EnumerateArray().Select(static i => i.GetProperty("id").GetGuid())];
    }

    private async Task ConcederConfigurarAsync(string sub, Guid unidade)
    {
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(_fixture.Factory.Services, PermissaoSelecao.ProcessosConfigurar.Codigo);
        await ConcessoesDeTeste.ConcederAsync(_fixture.Factory.Services, sub, perfil, EscopoDaConcessao.DaUnidade(unidade));
    }

    private async Task<Guid> SemearRascunhoAsync(string nome)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
        return processo.Id;
    }

    private async Task<Guid> SemearPublicadoAsync(string nome)
    {
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        Guid processoId;
        Guid documentoId;
        await using (AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder
                .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
            processoId = processo.Id;
            documentoId = documento.Id;
        }

        using HttpResponseMessage publicacao = await EnviarAsync(
            null, HttpMethod.Post, $"{Rota}/{processoId}/publicacao", "plataforma-admin",
            new
            {
                numero = "001/2026",
                documentoEditalId = documentoId,
                ato = new
                {
                    orgao = "CEPS",
                    serie = "EDITAL",
                    ano = 2026,
                    dataPublicacao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    assinante = "Diretor do CEPS",
                    tipoAtoCodigo = "EDITAL_ABERTURA",
                },
            });
        publicacao.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicacao.Content.ReadAsStringAsync());
        return processoId;
    }

    private async Task<Guid> UnidadeDoProcessoAsync(Guid processoId)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SelecaoDbContext>().ProcessosSeletivos.AsNoTracking()
            .Where(p => p.Id == processoId).Select(p => p.UnidadeAdministradoraOrigemId).SingleAsync();
    }

    private Task<HttpResponseMessage> EnviarAsync(string? sub, HttpMethod metodo, string rota, string? papel = null, object? corpo = null, string? ifMatch = null) =>
        EnviarCompletoAsync(sub, metodo, rota, papel, corpo, ifMatch);

    private async Task<HttpResponseMessage> EnviarCompletoAsync(string? sub, HttpMethod metodo, string rota, string? papel, object? corpo, string? ifMatch)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(metodo, new Uri(rota, UriKind.Relative));
        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        if (sub is not null || papel is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        }

        if (sub is not null)
        {
            request.Headers.TryAddWithoutValidation(TestAuthHandler.UserIdHeader, sub);
        }

        if (papel is not null)
        {
            request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, papel);
        }

        if (ifMatch is not null)
        {
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        if (metodo != HttpMethod.Get)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        }

        return await client.SendAsync(request);
    }
}
