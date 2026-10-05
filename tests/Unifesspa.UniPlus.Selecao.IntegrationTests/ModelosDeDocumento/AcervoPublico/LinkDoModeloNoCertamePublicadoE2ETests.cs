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

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O modelo baixado pelo candidato, de ponta a ponta: o edital é publicado pela API, o ato é
/// registrado pela fila, a divulgação copia o modelo para o acervo, e o link que o contrato público
/// do certame divulga devolve, a quem não tem credencial nenhuma, o arquivo que o edital congelou.
/// </summary>
[Collection(AcervoPublicoE2ECollection.Name)]
public sealed class LinkDoModeloNoCertamePublicadoE2ETests(AcervoPublicoE2EFixture fixture)
{
    [Fact(DisplayName = "Publicado o edital, o link do modelo no certame público baixa anonimamente o arquivo congelado")]
    public async Task LinkDoModelo_BaixaAnonimamenteOArquivoCongelado()
    {
        AcervoPublicoApiFactory api = fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        byte[] odt = ArquivosDeModeloDeTeste.Odt();
        string hash = Convert.ToHexStringLower(SHA256.HashData(odt));
        (Guid processoId, Guid documentoEditalId) = await SemearProcessoComModeloAsync(api, odt, hash);
        using HttpClient client = api.CreateClient();

        using (HttpResponseMessage publicacao = await PublicarAsync(client, processoId, documentoEditalId))
        {
            publicacao.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicacao.Content.ReadAsStringAsync());
        }

        await EsperaDeAtoRegistrado.AguardarAsync(api, await AtoDaVersaoAsync(api, processoId), "ato da abertura", processoId);
        Uri urlDownload = await UrlDoModeloNoCertameAsync(client, processoId);

        using HttpClient anonimo = new();
        byte[] baixado = await anonimo.GetByteArrayAsync(urlDownload);
        Convert.ToHexStringLower(SHA256.HashData(baixado)).Should().Be(hash,
            "o candidato baixa exatamente o arquivo cujo hash o edital congelou");
    }

    /// <summary>
    /// O processo publicável com uma exigência que oferece um modelo confirmado — a cópia selada no
    /// bucket privado, como a confirmação do envio a deixa.
    /// </summary>
    private static async Task<(Guid ProcessoId, Guid DocumentoEditalId)> SemearProcessoComModeloAsync(
        AcervoPublicoApiFactory api, byte[] conteudo, string hash)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        ModeloDeDocumento? modelo = null;

        (ProcessoSeletivo processo, DocumentoEdital documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
            db,
            $"PS com modelo no acervo {Guid.CreateVersion7()}",
            complementar: processo =>
            {
                modelo = ModeloDeDocumento.IniciarPendente(
                    processo.Id, "Autodeclaração étnico-racial", "ODT", TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
                modelo.Confirmar(conteudo.LongLength, hash, TimeProvider.System).IsSuccess.Should().BeTrue();
                db.ModelosDeDocumento.Add(modelo);

                DocumentoExigido exigencia = DocumentoExigido.Criar(
                    processo.CronogramaFases.Single().Id,
                    tipoDocumentoOrigemId: Guid.CreateVersion7(),
                    tipoDocumentoCodigo: "AUTODECLARACAO_ETNICO_RACIAL",
                    tipoDocumentoNome: "Autodeclaração étnico-racial",
                    tipoDocumentoCategoria: "PESSOAL",
                    aplicabilidade: Aplicabilidade.Geral,
                    obrigatorio: true,
                    consequenciaIndeferimento: null,
                    condicoes: [],
                    basesLegais: [DocumentoExigidoBaseLegal.Criar(
                        "Res. Unifesspa 532/2021, art. 12", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, null).Value!],
                    idadeMaximaEmissao: null,
                    formatosPermitidos: FormatosPermitidos.Criar(true, null).Value!,
                    tamanhoMaximoBytes: null,
                    modelo: ModeloDaExigencia.Do(modelo).Value!,
                    finalidade: FinalidadeFormulario.Inscricao).Value!;
                processo.DefinirDocumentosExigidos([NoExigencia.CriarFolha(exigencia, 0).Value!], PrecondicaoIfMatch.Ausente)
                    .IsSuccess.Should().BeTrue();
            });

        await scope.ServiceProvider.GetRequiredService<IArquivoArmazenadoStorage>()
            .SalvarConteudoSeladoAsync(modelo!.ObjectKeyConfirmado!, conteudo, modelo.ContentType);

        return (processo.Id, documento.Id);
    }

    private static async Task<HttpResponseMessage> PublicarAsync(HttpClient client, Guid processoId, Guid documentoEditalId)
    {
        using HttpRequestMessage request = new(
            HttpMethod.Post, new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                numero = "001/2027",
                documentoEditalId,
                ato = new
                {
                    orgao = "CEPS",
                    serie = "EDITAL",
                    ano = 2027,
                    dataPublicacao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    assinante = "Diretor do CEPS",
                    tipoAtoCodigo = DadosDoAtoDeTeste.TipoAbertura,
                },
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }

    private static async Task<Guid> AtoDaVersaoAsync(AcervoPublicoApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        return await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .Select(v => v.AtoCriadorId)
            .SingleAsync();
    }

    /// <summary>
    /// O link do modelo no contrato público do certame, lido sem credencial. A divulgação chega pela
    /// fila depois do registro do ato, então o certame pode ainda responder 404 por alguns instantes.
    /// </summary>
    private static async Task<Uri> UrlDoModeloNoCertameAsync(HttpClient client, Guid processoId)
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
        while (true)
        {
            using HttpResponseMessage resposta = await client.GetAsync(
                new Uri($"/api/selecao/certames/{processoId}", UriKind.Relative), CancellationToken.None);
            if (resposta.StatusCode == HttpStatusCode.OK)
            {
                using JsonDocument certame = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
                return new Uri(certame.RootElement.GetProperty("documentosExigidos").EnumerateArray()
                    .Single()
                    .GetProperty("modelo")
                    .GetProperty("urlDownload")
                    .GetString()!);
            }

            DateTimeOffset.UtcNow.Should().BeBefore(limite, "a divulgação do certame não chegou depois do registro do ato");
            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }
    }
}
