namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Wolverine;

/// <summary>
/// O registro do ato do processo contra o host real (Wolverine + PG queue durável + Postgres): é
/// gravado junto da requisição de registro, passa a registrado quando o Publicações confirma o ato,
/// e aparece na consulta administrativa.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class RegistroDoAtoDoProcessoTests
{
    private readonly CascadingFixture _fixture;

    public RegistroDoAtoDoProcessoTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O registro nasce pendente com a requisição e passa a registrado quando o Publicações confirma o ato; a repetição não muda nada")]
    public async Task Registro_PassaARegistradoQuandoOAtoEConfirmado()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid documentoId) = await SemearDivulgadoAsync(api);
        Guid atoId = Guid.CreateVersion7();

        await using (AsyncServiceScope escopo = api.Services.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<IMessageBus>()
                .InvokeAsync(new RegistrarAtoDoProcessoDeTesteCommand(atoId, processoId, documentoId, FalharDepoisDeGravar: false));
        }

        // O Publicações registra o ato e devolve a confirmação pela fila durável.
        await EsperaDeAtoRegistrado.AguardarAsync(api, atoId, "ato do registro do processo", processoId);
        AtoDoProcesso registrado = await AguardarSituacaoAsync(api, atoId, SituacaoDoRegistroDoAto.Registrado);
        registrado.RegistradoEm.Should().NotBeNull();

        await using AsyncServiceScope repeticao = api.Services.CreateAsyncScope();
        await repeticao.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync(new AtoNormativoRegistrado(atoId, 2026, DadosDoAtoDeTeste.TipoAviso));

        AtoDoProcesso depois = await LerAsync(api, atoId);
        depois.RegistradoEm.Should().Be(registrado.RegistradoEm, "a entrega repetida da confirmação não muda o registro");
    }

    [Fact(DisplayName = "Falha depois de gravar o registro não deixa registro nem envio: os dois andam na mesma transação")]
    public async Task FalhaDepoisDeGravar_NaoDeixaRegistro()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid documentoId) = await SemearAsync(api);
        Guid atoId = Guid.CreateVersion7();

        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        Func<Task> falhar = () => escopo.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync(new RegistrarAtoDoProcessoDeTesteCommand(atoId, processoId, documentoId, FalharDepoisDeGravar: true));

        await falhar.Should().ThrowAsync<InvalidOperationException>().WithMessage(RegistrarAtoDoProcessoDeTesteHandler.MensagemErro);

        await using AsyncServiceScope leitura = api.Services.CreateAsyncScope();
        SelecaoDbContext db = leitura.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (await db.AtosDoProcesso.AsNoTracking().AnyAsync(a => a.Id == atoId)).Should().BeFalse(
            "a transação inteira volta: o registro some junto da requisição que não chegou a sair");
    }

    [Fact(DisplayName = "Confirmação de ato que não é do processo é ignorada")]
    public async Task ConfirmacaoDeAtoDeOutroDominio_EIgnorada()
    {
        CascadingApiFactory api = _fixture.Factory;
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();

        Func<Task> entregar = () => escopo.ServiceProvider.GetRequiredService<IMessageBus>()
            .InvokeAsync(new AtoNormativoRegistrado(Guid.CreateVersion7(), 2026, "CONVOCACAO"));

        await entregar.Should().NotThrowAsync();
    }

    [Fact(DisplayName = "A consulta administrativa devolve o ato do processo com situação e documento")]
    public async Task Consulta_DevolveOAtoComSituacaoEDocumento()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid documentoId) = await SemearDivulgadoAsync(api);
        Guid atoId = Guid.CreateVersion7();
        await using (AsyncServiceScope escopo = api.Services.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<IMessageBus>()
                .InvokeAsync(new RegistrarAtoDoProcessoDeTesteCommand(atoId, processoId, documentoId, FalharDepoisDeGravar: false));
        }

        await AguardarSituacaoAsync(api, atoId, SituacaoDoRegistroDoAto.Registrado);

        using HttpRequestMessage pedido = new(HttpMethod.Get, new Uri($"/api/selecao/processos-seletivos/{processoId}/atos", UriKind.Relative));
        pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        pedido.Headers.TryAddWithoutValidation("Accept", "application/vnd.uniplus.ato-do-processo.v1+json");
        using HttpResponseMessage resposta = await client.SendAsync(pedido);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        JsonElement ato = corpo.RootElement.EnumerateArray().Single();
        ato.GetProperty("id").GetGuid().Should().Be(atoId);
        ato.GetProperty("tipoCodigo").GetString().Should().Be(DadosDoAtoDeTeste.TipoAviso);
        ato.GetProperty("situacao").GetString().Should().Be("registrado");
        ato.GetProperty("pendenciaAcimaDoLimiar").GetBoolean().Should().BeFalse();
        ato.GetProperty("documentoPrincipal").GetProperty("documentoAPublicarId").GetGuid().Should().Be(documentoId);
    }

    [Theory(DisplayName = "Publicar o edital registra o ato do processo com o edital como principal e o confirma quando o Publicações registra o ato")]
    [InlineData("001/2026", "001/2026")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public async Task Publicar_RegistraOAtoDoEditalComOEditalComoPrincipal(string numeroDeclarado, string? numeroGravado)
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid documentoId) = await SemearAsync(api);

        using HttpRequestMessage pedido = new(HttpMethod.Post, new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative))
        {
            Content = System.Net.Http.Json.JsonContent.Create(new
            {
                numero = numeroDeclarado,
                documentoEditalId = documentoId,
                ato = new
                {
                    orgao = "CEPS",
                    serie = "EDITAL",
                    ano = 2026,
                    dataPublicacao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    assinante = "Diretor do CEPS",
                    tipoAtoCodigo = DadosDoAtoDeTeste.TipoAbertura,
                },
            }),
        };
        pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        (await client.SendAsync(pedido)).StatusCode.Should().Be(HttpStatusCode.NoContent, "o número em branco é ausência de número, não erro");

        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        Guid atoId = await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .Select(v => v.AtoCriadorId)
            .SingleAsync();

        AtoDoProcesso registrado = await AguardarSituacaoAsync(api, atoId, SituacaoDoRegistroDoAto.Registrado);
        registrado.DocumentoPrincipalId.Should().Be(documentoId);
        registrado.TipoCodigo.Should().Be(DadosDoAtoDeTeste.TipoAbertura);
        registrado.Numero.Should().Be(numeroGravado);
        registrado.Assinante.Should().Be("Diretor do CEPS");
        registrado.AtoCorrigidoId.Should().BeNull();
    }

    [Fact(DisplayName = "A consulta devolve do ato registrado mais recentemente para o mais antigo")]
    public async Task Consulta_OrdenaDoMaisRecenteParaOMaisAntigo()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid primeiroDocumento) = await SemearDivulgadoAsync(api);
        Guid segundoDocumento = await SemearDocumentoAsync(api, processoId);
        Guid primeiro = Guid.CreateVersion7();
        Guid segundo = Guid.CreateVersion7();

        foreach ((Guid ato, Guid documento) in new[] { (primeiro, primeiroDocumento), (segundo, segundoDocumento) })
        {
            await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
            await escopo.ServiceProvider.GetRequiredService<IMessageBus>()
                .InvokeAsync(new RegistrarAtoDoProcessoDeTesteCommand(ato, processoId, documento, FalharDepoisDeGravar: false));
            // Instantes de gravação distintos: o relógio do carimbo tem resolução finita.
            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }

        using HttpRequestMessage pedido = new(HttpMethod.Get, new Uri($"/api/selecao/processos-seletivos/{processoId}/atos", UriKind.Relative));
        pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        pedido.Headers.TryAddWithoutValidation("Accept", "application/vnd.uniplus.ato-do-processo.v1+json");
        using HttpResponseMessage resposta = await client.SendAsync(pedido);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        corpo.RootElement.EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).Should().Equal(segundo, primeiro);
    }

    [Fact(DisplayName = "A consulta dos itens previstos lê o cronograma congelado e passa o item a publicado quando o ato dele se registra")]
    public async Task ItensPrevistos_LeOCronogramaCongeladoEAcompanhaORegistro()
    {
        const string tipoDoItem = "RESULTADO_CLASSIFICACAO";
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        await SemearTipoDeResultadoAsync(api, tipoDoItem);
        (Guid processoId, Guid documentoId) = await SemearAsync(api);
        await PublicarEditalAsync(client, processoId, documentoId);

        JsonElement antes = await ConsultarItensPrevistosAsync(client, processoId);

        JsonElement item = antes.GetProperty("fases").EnumerateArray().Single().GetProperty("produtos").EnumerateArray().Single();
        item.GetProperty("tipoCodigo").GetString().Should().Be(tipoDoItem);
        item.GetProperty("tipoNome").GetString().Should().Be("Resultado final");
        item.GetProperty("papel").GetString().Should().Be("DEFINITIVO");
        item.GetProperty("situacao").GetString().Should().Be("aberto");
        item.GetProperty("podePublicar").GetBoolean().Should().BeTrue();

        Guid produtoId = item.GetProperty("id").GetGuid();
        Guid atoId = Guid.CreateVersion7();
        await using (AsyncServiceScope escopo = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            AtoDoProcesso ato = AtoDoProcesso.Registrar(
                new NovoAtoDoProcesso(
                    atoId, processoId, tipoDoItem, "Resultado final", "7/2027", new DateOnly(2027, 1, 20),
                    "Diretor do CEPS", "ceps", "psm-medicina-2027", documentoId, [], ProdutoDoEditalId: produtoId),
                TimeProvider.System).Value!;
            ato.ConfirmarRegistro(TimeProvider.System);
            db.AtosDoProcesso.Add(ato);
            await db.SaveChangesAsync();
        }

        JsonElement depois = await ConsultarItensPrevistosAsync(client, processoId);

        JsonElement publicado = depois.GetProperty("fases").EnumerateArray().Single().GetProperty("produtos").EnumerateArray().Single();
        publicado.GetProperty("situacao").GetString().Should().Be("publicado");
        publicado.GetProperty("ato").GetProperty("id").GetGuid().Should().Be(atoId);
        publicado.GetProperty("ato").GetProperty("documentoPrincipal").GetProperty("documentoAPublicarId").GetGuid().Should().Be(documentoId);
        publicado.GetProperty("podePublicar").GetBoolean().Should().BeFalse();
    }

    private static async Task SemearTipoDeResultadoAsync(CascadingApiFactory api, string codigo)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        PublicacoesDbContext db = escopo.ServiceProvider.GetRequiredService<PublicacoesDbContext>();
        if (await db.Set<TipoAtoPublicado>().AnyAsync(t => t.Codigo == codigo))
        {
            return;
        }

        db.Set<TipoAtoPublicado>().Add(TipoAtoPublicado.Criar(
            codigo, "Resultado final", congelaConfiguracao: false, unicoPorObjeto: false, efeitoIrreversivel: false, ehResultado: true,
            new DateOnly(2020, 1, 1), vigenciaFim: null, baseLegal: null).Value!);
        await db.SaveChangesAsync();
    }

    private static async Task PublicarEditalAsync(HttpClient client, Guid processoId, Guid documentoId)
    {
        using HttpRequestMessage pedido = new(HttpMethod.Post, new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative))
        {
            Content = System.Net.Http.Json.JsonContent.Create(new
            {
                numero = "001/2027",
                documentoEditalId = documentoId,
                ato = new
                {
                    orgao = "CEPS",
                    serie = "EDITAL",
                    ano = 2027,
                    dataPublicacao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    assinante = "Diretor do CEPS",
                    tipoAtoCodigo = DadosDoAtoDeTeste.TipoAbertura,
                },
            }),
        };
        pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        (await client.SendAsync(pedido)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task<JsonElement> ConsultarItensPrevistosAsync(HttpClient client, Guid processoId)
    {
        using HttpRequestMessage pedido = new(HttpMethod.Get, new Uri($"/api/selecao/processos-seletivos/{processoId}/produtos-previstos", UriKind.Relative));
        pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        pedido.Headers.TryAddWithoutValidation("Accept", "application/vnd.uniplus.produtos-previstos-do-processo.v1+json");
        using HttpResponseMessage resposta = await client.SendAsync(pedido);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static async Task<Guid> SemearDocumentoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            processoId, Domain.Enums.FinalidadeDoDocumento.DocumentoDeAto, "aviso.pdf", Domain.Services.TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, new string('d', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        db.DocumentosAPublicar.Add(documento);
        await db.SaveChangesAsync();
        return documento.Id;
    }

    /// <summary>
    /// O processo com a divulgação do certame já materializada: o documento do ato que não cria versão
    /// só é divulgado numa divulgação que existe, e a confirmação do ato anda na mesma entrega.
    /// </summary>
    private static async Task<(Guid ProcessoId, Guid DocumentoId)> SemearDivulgadoAsync(CascadingApiFactory api)
    {
        (Guid processoId, Guid documentoId) = await SemearAsync(api);
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        DateTimeOffset agora = DateTimeOffset.UtcNow;
        db.CertamesDivulgados.Add(CertameDivulgado.Criar(
            processoId,
            1,
            Guid.CreateVersion7(),
            new string('a', 64),
            "teste",
            new FacetasDoCertameDivulgado($"certame-{Guid.NewGuid():N}"[..40], "Certame de teste", null, [], agora, agora.AddDays(30)),
            "{}",
            agora));
        await db.SaveChangesAsync();
        return (processoId, documentoId);
    }

    private static async Task<(Guid ProcessoId, Guid DocumentoId)> SemearAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, $"Atos do processo {Guid.CreateVersion7()}");
        return (processo.Id, documento.Id);
    }

    private static async Task<AtoDoProcesso> LerAsync(CascadingApiFactory api, Guid atoId)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        return await db.AtosDoProcesso.AsNoTracking().SingleAsync(a => a.Id == atoId);
    }

    private static async Task<AtoDoProcesso> AguardarSituacaoAsync(CascadingApiFactory api, Guid atoId, SituacaoDoRegistroDoAto esperada)
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            AtoDoProcesso ato = await LerAsync(api, atoId);
            if (ato.Situacao == esperada || DateTimeOffset.UtcNow >= limite)
            {
                ato.Situacao.Should().Be(esperada, "a confirmação do Publicações chega pela fila durável");
                return ato;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }
    }
}
