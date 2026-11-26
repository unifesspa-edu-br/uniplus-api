namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ModelosDeDocumento.AcervoPublico;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O edital e o modelo baixados pelo candidato, de ponta a ponta: o edital é publicado pela API, o
/// ato é registrado pela fila, a divulgação copia os dois documentos para o acervo, e o link que o
/// contrato público do certame divulga devolve, a quem não tem credencial nenhuma, o arquivo que o
/// edital congelou.
/// </summary>
[Collection(AcervoPublicoE2ECollection.Name)]
public sealed class LinkDoModeloNoCertamePublicadoE2ETests(AcervoPublicoE2EFixture fixture)
{
    /// <summary>O conteúdo da cópia selada do documento do edital, como a confirmação do envio a deixa.</summary>
    private static readonly byte[] PdfDoEdital = "%PDF-1.4\n% edital\n%%EOF\n"u8.ToArray();

    [Fact(DisplayName = "Publicado o edital, os links do edital e do modelo no certame público baixam anonimamente os arquivos")]
    public async Task LinkDoModelo_BaixaAnonimamenteOArquivoCongelado()
    {
        AcervoPublicoApiFactory api = fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        byte[] odt = ArquivosDeModeloDeTeste.Odt();
        string hash = Convert.ToHexStringLower(SHA256.HashData(odt));
        (Guid processoId, Guid documentoEditalId) = await SemearProcessoComModeloAsync(api, odt, hash);
        using HttpClient client = api.CreateClient();

        await PublicarComSucessoAsync(client, processoId, documentoEditalId);

        await AtoNoRegistroCentral.LerAsync(api, await AtoDaVersaoAsync(api, processoId));
        using JsonDocument certame = await CertameDivulgadoAsync(client, processoId);
        await ConferirDownloadsAsync(certame, hash);
    }


    private static async Task PublicarComSucessoAsync(HttpClient client, Guid processoId, Guid documentoEditalId)
    {
        using HttpResponseMessage publicacao = await PublicarAsync(client, processoId, documentoEditalId);
        publicacao.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicacao.Content.ReadAsStringAsync());
    }

    private static async Task ConferirDownloadsAsync(JsonDocument certame, string hash)
    {
        Uri urlDoModelo = new(certame.RootElement.GetProperty("documentosExigidos").EnumerateArray()
            .Single().GetProperty("modelo").GetProperty("urlDownload").GetString()!);
        Uri urlDoEdital = new(certame.RootElement.GetProperty("documentoEdital").GetProperty("urlDownload").GetString()!);
        using HttpClient anonimo = new();
        Convert.ToHexStringLower(SHA256.HashData(await anonimo.GetByteArrayAsync(urlDoModelo))).Should().Be(hash);
        (await anonimo.GetByteArrayAsync(urlDoEdital)).Should().Equal(PdfDoEdital);
    }

    /// <summary>
    /// O processo publicável com uma exigência que oferece um modelo confirmado — a cópia selada no
    /// bucket privado, como a confirmação do envio a deixa.
    /// </summary>
    private static async Task<(Guid ProcessoId, Guid DocumentoEditalId)> SemearProcessoComModeloAsync(
        AcervoPublicoApiFactory api, byte[] conteudo, string hash)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ModeloDeDocumento? modelo = null;

        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel
        {
            Nome = $"PS com modelo no acervo {Guid.CreateVersion7()}",
            Complementar = processo => modelo = OferecerModeloConfirmado(db, processo, conteudo, hash),
        });
        await SelarOsConteudosNoStorageAsync(scope.ServiceProvider, modelo!, documento, conteudo);

        return (processo.Id, documento.Id);
    }

    private static async Task SelarOsConteudosNoStorageAsync(
        IServiceProvider servicos, ModeloDeDocumento modelo, DocumentoAPublicar documento, byte[] conteudo)
    {
        IArquivoArmazenadoStorage storage = servicos.GetRequiredService<IArquivoArmazenadoStorage>();
        await storage.SalvarConteudoSeladoAsync(modelo.ObjectKeyConfirmado!, conteudo, modelo.ContentType);
        await storage.SalvarConteudoSeladoAsync(documento.ObjectKeyConfirmado!, PdfDoEdital, TiposDeMidiaDeDocumento.Pdf);
    }

    private static ModeloDeDocumento OferecerModeloConfirmado(UniPlusDbContext db, ProcessoSeletivo processo, byte[] conteudo, string hash)
    {
        ModeloDeDocumento modelo = ModeloDeDocumento.IniciarPendente(
            processo.Id, "Autodeclaração étnico-racial", "ODT", TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        modelo.Confirmar(conteudo.LongLength, hash, TimeProvider.System).IsSuccess.Should().BeTrue();
        db.ModelosDeDocumento.Add(modelo);
        processo.DefinirDocumentosExigidos([RegraDeComprovacao.CriarRegraDeDocumento(ExigenciaQueOfereceOModelo(processo, modelo), 0).Value!])
            .IsSuccess.Should().BeTrue();

        return modelo;
    }

    private static DocumentoExigido ExigenciaQueOfereceOModelo(ProcessoSeletivo processo, ModeloDeDocumento modelo) => DocumentoExigido.Criar(
        Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("exigencia_autodeclaracao_etnico_racial").Value!,
        new DocumentoExigidoDeclarado(new CopiaDoTipoDeDocumento(TipoDocumentoSeed.Itens[0].Id, "AUTODECLARACAO_ETNICO_RACIAL", "Autodeclaração étnico-racial", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal),
            new MomentoDaExigencia(processo.CronogramaFases.Single().Id, null, FormularioDeTeste.CodigoDoPapel(FinalidadeFormulario.Inscricao)),
            new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []),
            new IndeferimentoDoDocumento(true, null, [DocumentoExigidoBaseLegal.Criar("Res. Unifesspa 532/2021, art. 12", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, null).Value!]),
            RequisitosDoDocumentoDeTeste.Com(FormatosPermitidos.Criar(true, null).Value!, modelo: ModeloDaExigencia.Do(modelo).Value!),
            null)).Value!;

    private static async Task<HttpResponseMessage> PublicarAsync(HttpClient client, Guid processoId, Guid documentoEditalId)
    {
        using HttpRequestMessage request = CriarPedidoDePublicacao(processoId, documentoEditalId);
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }

    private static HttpRequestMessage CriarPedidoDePublicacao(Guid processoId, Guid documentoEditalId)
    {
        return new HttpRequestMessage(
            HttpMethod.Post, new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                numero = "001/2027",
                documentoEditalId,
                ato = CriarAto(),
            }),
        };
    }

    private static object CriarAto() => new
    {
        orgao = "CEPS",
        serie = "EDITAL",
        ano = 2027,
        dataDoAto = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        assinante = "Diretor do CEPS",
        tipoAtoCodigo = DadosDoAtoDeTeste.TipoAbertura,
    };

    private static async Task<Guid> AtoDaVersaoAsync(AcervoPublicoApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .Select(v => v.AtoCriadorId)
            .SingleAsync();
    }

    /// <summary>
    /// O contrato público do certame, lido sem credencial. A divulgação chega pela fila depois do
    /// registro do ato, então o certame pode ainda responder 404 por alguns instantes.
    /// </summary>
    /// <summary>O certame existe com a publicação, mas o endereço dos documentos só chega quando a cópia ao acervo termina.</summary>
    private static async Task<JsonDocument> CertameDivulgadoAsync(HttpClient client, Guid processoId)
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            using HttpResponseMessage resposta = await client.GetAsync(new Uri($"/api/selecao/certames/{processoId}", UriKind.Relative));
            JsonDocument? certame = resposta.StatusCode == HttpStatusCode.OK ? await ComEnderecoDoEditalAsync(resposta) : null;
            if (certame is not null)
            {
                return certame;
            }

            DateTimeOffset.UtcNow.Should().BeBefore(limite, "a cópia dos documentos ao acervo público não terminou depois da publicação");
            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }
    }

    private static async Task<JsonDocument?> ComEnderecoDoEditalAsync(HttpResponseMessage resposta)
    {
        JsonDocument certame = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        if (certame.RootElement.GetProperty("documentoEdital").GetProperty("urlDownload").ValueKind == JsonValueKind.String)
        {
            return certame;
        }

        certame.Dispose();
        return null;
    }
}
