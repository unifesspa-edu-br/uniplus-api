namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;
using Domain.ValueObjects;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O registro do ato do processo contra o host real: a publicação o grava com o ato normativo na mesma
/// transação, e ele aparece na consulta administrativa e na dos itens previstos.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class RegistroDoAtoDoProcessoTests
{
    private readonly CascadingFixture _fixture;

    public RegistroDoAtoDoProcessoTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "A consulta administrativa devolve o ato do processo com o instante da divulgação e o documento")]
    public async Task Consulta_DevolveOAtoComOInstanteEODocumento()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid documentoId) = await SemearDivulgadoAsync(api);
        Guid atoId = Guid.CreateVersion7();
        DateTimeOffset publicadoEm = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
        await SemearAtoAsync(api, atoId, processoId, documentoId, publicadoEm);

        IReadOnlyList<JsonElement> atos = await PublicarAtoAvulsoEndpointTests.ConsultarAtosAsync(client, processoId);

        AfirmarAtoConsultado(atos.Single(), atoId, documentoId, publicadoEm);
    }

    private static void AfirmarAtoConsultado(JsonElement ato, Guid atoId, Guid documentoId, DateTimeOffset publicadoEm)
    {
        ato.GetProperty("id").GetGuid().Should().Be(atoId);
        ato.GetProperty("tipoCodigo").GetString().Should().Be(DadosDoAtoDeTeste.TipoAviso);
        ato.GetProperty("publicadoEm").GetDateTimeOffset().Should().Be(publicadoEm);
        ato.TryGetProperty("situacao", out _).Should().BeFalse("o ato nasce divulgado: não há estado pendente a expor");
        ato.TryGetProperty("pendenciaAcimaDoLimiar", out _).Should().BeFalse();
        ato.GetProperty("documentoPrincipal").GetProperty("documentoAPublicarId").GetGuid().Should().Be(documentoId);
    }

    [Theory(DisplayName = "Publicar o edital registra o ato do processo com o edital como principal, e o ato normativo nasce na mesma transação")]
    [InlineData("001/2026", "001/2026")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    public async Task Publicar_RegistraOAtoDoEditalComOEditalComoPrincipal(string numeroDeclarado, string? numeroGravado)
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid documentoId) = await SemearAsync(api);

        await PublicarEditalAsync(client, processoId, documentoId, numeroDeclarado, 2026);

        Guid atoId = await AtoCriadorDaVersaoAsync(api, processoId);
        AtoDoProcesso registrado = await LerAsync(api, atoId);
        await AtoNoRegistroCentral.LerAsync(api, atoId);
        AfirmarEditalComoPrincipal(registrado, documentoId, numeroGravado);
    }

    private static void AfirmarEditalComoPrincipal(AtoDoProcesso registrado, Guid documentoId, string? numeroGravado)
    {
        registrado.DocumentoPrincipalId.Should().Be(documentoId);
        registrado.TipoCodigo.Should().Be(DadosDoAtoDeTeste.TipoAbertura);
        registrado.Numero.Should().Be(numeroGravado);
        registrado.Assinante.Should().Be("Diretor do CEPS");
        registrado.AtoCorrigidoId.Should().BeNull();
    }

    private static async Task<Guid> AtoCriadorDaVersaoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .Select(v => v.AtoCriadorId)
            .SingleAsync();
    }

    [Fact(DisplayName = "A consulta devolve do ato publicado mais recentemente para o mais antigo")]
    public async Task Consulta_OrdenaDoMaisRecenteParaOMaisAntigo()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid primeiroDocumento) = await SemearDivulgadoAsync(api);
        Guid segundoDocumento = await SemearDocumentoAsync(api, processoId);
        Guid primeiro = Guid.CreateVersion7();
        Guid segundo = Guid.CreateVersion7();
        DateTimeOffset instanteBase = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);
        await SemearAtoAsync(api, primeiro, processoId, primeiroDocumento, instanteBase);
        await SemearAtoAsync(api, segundo, processoId, segundoDocumento, instanteBase.AddMinutes(5));

        IReadOnlyList<JsonElement> atos = await PublicarAtoAvulsoEndpointTests.ConsultarAtosAsync(client, processoId);

        atos.Select(a => a.GetProperty("id").GetGuid()).Should().Equal(segundo, primeiro);
    }

    [Fact(DisplayName = "A consulta dos itens previstos lê o cronograma congelado e passa o item a publicado quando o ato dele é publicado")]
    public async Task ItensPrevistos_LeOCronogramaCongeladoEAcompanhaAPublicacao()
    {
        const string tipoDoItem = "RESULTADO_CLASSIFICACAO";
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        await SemearTipoDeResultadoAsync(api, tipoDoItem);
        (Guid processoId, Guid documentoId) = await SemearAsync(api);
        await PublicarEditalAsync(client, processoId, documentoId, "001/2027", 2027);

        JsonElement item = await ItemPrevistoUnicoAsync(client, processoId);
        AfirmarItemAberto(item, tipoDoItem);
        Guid atoId = await RegistrarAtoDoItemAsync(api, processoId, documentoId, tipoDoItem, item.GetProperty("id").GetGuid());

        JsonElement publicado = await ItemPrevistoUnicoAsync(client, processoId);
        AfirmarItemPublicado(publicado, atoId, documentoId);
    }

    private static void AfirmarItemAberto(JsonElement item, string tipoDoItem)
    {
        item.GetProperty("tipoCodigo").GetString().Should().Be(tipoDoItem);
        item.GetProperty("tipoNome").GetString().Should().Be("Resultado final");
        item.GetProperty("papel").GetProperty("codigo").GetString().Should().Be("DEFINITIVO");
        item.GetProperty("situacao").GetProperty("codigo").GetString().Should().Be("ABERTO");
        item.GetProperty("podePublicar").GetBoolean().Should().BeTrue();
    }

    private static void AfirmarItemPublicado(JsonElement publicado, Guid atoId, Guid documentoId)
    {
        publicado.GetProperty("situacao").GetProperty("codigo").GetString().Should().Be("PUBLICADO");
        publicado.GetProperty("ato").GetProperty("id").GetGuid().Should().Be(atoId);
        publicado.GetProperty("ato").GetProperty("documentoPrincipal").GetProperty("documentoAPublicarId").GetGuid().Should().Be(documentoId);
        publicado.GetProperty("podePublicar").GetBoolean().Should().BeFalse();
    }

    private static async Task<JsonElement> ItemPrevistoUnicoAsync(HttpClient client, Guid processoId)
    {
        JsonElement itens = await ConsultarItensPrevistosAsync(client, processoId);
        return itens.GetProperty("fases").EnumerateArray().Single().GetProperty("produtos").EnumerateArray().Single();
    }

    private static async Task<Guid> RegistrarAtoDoItemAsync(CascadingApiFactory api, Guid processoId, Guid documentoId, string tipoDoItem, Guid itemId)
    {
        IdentidadeDoItemDoEdital identidadeDoItem = await IdentidadeDoItemNoBanco.DoItemAsync(api.Services, processoId, itemId);
        Guid atoId = Guid.CreateVersion7();
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        db.AtosDoProcesso.Add(AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                atoId, processoId, tipoDoItem, "Resultado final", "7/2027", new DateOnly(2027, 1, 20),
                "Diretor do CEPS", "ceps", "psm-medicina-2027", documentoId, [], ItemDoEdital: identidadeDoItem),
            TimeProvider.System.GetUtcNow()).Value!);
        await db.SaveChangesAsync();
        return atoId;
    }

    private static async Task SemearTipoDeResultadoAsync(CascadingApiFactory api, string codigo)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        if (await db.Set<TipoAtoPublicado>().AnyAsync(t => t.Codigo == codigo))
        {
            return;
        }

        db.Set<TipoAtoPublicado>().Add(DeclaracaoDeResultadoFinal(codigo).Criar().Value!);
        await db.SaveChangesAsync();
    }

    private static TipoAtoPublicadoDeTeste DeclaracaoDeResultadoFinal(string codigo) =>
        new()
        {
            Codigo = codigo,
            Nome = "Resultado final",
            EhResultado = true,
            VigenciaInicio = new DateOnly(2020, 1, 1)
        };

    private static async Task PublicarEditalAsync(HttpClient client, Guid processoId, Guid documentoId, string numero, int ano)
    {
        using HttpRequestMessage pedido = PublicarAtoAvulsoEndpointTests.Autenticado(
            HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/publicacao");
        pedido.Content = CorpoDaPublicacaoDoEdital(documentoId, numero, ano);
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        using HttpResponseMessage resposta = await client.SendAsync(pedido);

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, await resposta.Content.ReadAsStringAsync());
    }

    private static System.Net.Http.Json.JsonContent CorpoDaPublicacaoDoEdital(Guid documentoId, string numero, int ano) =>
        System.Net.Http.Json.JsonContent.Create(new
        {
            numero,
            documentoEditalId = documentoId,
            ato = new
            {
                orgao = "CEPS",
                serie = "EDITAL",
                ano,
                dataDoAto = PublicarAtoAvulsoEndpointTests.HojeEmTexto(),
                assinante = "Diretor do CEPS",
                tipoAtoCodigo = DadosDoAtoDeTeste.TipoAbertura,
            },
        });

    private static async Task<JsonElement> ConsultarItensPrevistosAsync(HttpClient client, Guid processoId)
    {
        using HttpRequestMessage pedido = PublicarAtoAvulsoEndpointTests.Autenticado(
            HttpMethod.Get, $"/api/selecao/processos-seletivos/{processoId}/produtos-previstos");
        pedido.Headers.TryAddWithoutValidation("Accept", "application/vnd.uniplus.produtos-previstos-do-processo.v1+json");
        using HttpResponseMessage resposta = await client.SendAsync(pedido);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        using JsonDocument corpo = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return corpo.RootElement.Clone();
    }

    private static async Task<Guid> SemearDocumentoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
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
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        db.CertamesDivulgados.Add(CertameDeTeste(processoId));
        await db.SaveChangesAsync();
        return (processoId, documentoId);
    }

    private static CertameDivulgado CertameDeTeste(Guid processoId)
    {
        DateTimeOffset agora = DateTimeOffset.UtcNow;
        return CertameDivulgado.Criar(
            processoId,
            1,
            Guid.CreateVersion7(),
            new string('a', 64),
            "teste",
            new FacetasDoCertameDivulgado($"certame-{Guid.NewGuid():N}"[..40], "Certame de teste", null, [], agora, agora.AddDays(30)),
            "{}",
            agora);
    }

    private static async Task<(Guid ProcessoId, Guid DocumentoId)> SemearAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"Atos do processo {Guid.CreateVersion7()}" });
        return (processo.Id, documento.Id);
    }

    private static async Task SemearAtoAsync(CascadingApiFactory api, Guid atoId, Guid processoId, Guid documentoId, DateTimeOffset publicadoEm)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        AtoDoProcesso ato = AtoDoProcesso.Registrar(
            new NovoAtoDoProcesso(
                atoId, processoId, DadosDoAtoDeTeste.TipoAviso, "Aviso", "3/2026", DateOnly.FromDateTime(publicadoEm.UtcDateTime),
                "Diretor do CEPS", "ceps", "psm-medicina-2027", documentoId, []),
            publicadoEm).Value!;
        db.AtosDoProcesso.Add(ato);
        await db.SaveChangesAsync();
    }

    private static async Task<AtoDoProcesso> LerAsync(CascadingApiFactory api, Guid atoId)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await db.AtosDoProcesso.AsNoTracking().SingleAsync(a => a.Id == atoId);
    }
}
