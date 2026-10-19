namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// A publicação do documento avulso e da correção de um ato, de ponta a ponta contra o host real:
/// o comando grava o registro do ato do processo e envia a requisição pela fila, o Publicações
/// registra, a divulgação copia os arquivos e o ato aparece na página pública do certame.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class PublicarAtoAvulsoEndpointTests
{
    private readonly CascadingFixture _fixture;

    public PublicarAtoAvulsoEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "O aviso com anexo aparece registrado na consulta administrativa e com principal e anexo na página pública; a mesma chave de idempotência não cria dois atos")]
    public async Task Aviso_ComAnexo_AparecePublicoEARepeticaoNaoCriaOutro()
    {
        (CascadingApiFactory api, HttpClient client) = await Preparar();
        (Guid processoId, _) = await PublicarEditalAsync(api, client);
        Guid principal = await SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "aviso.pdf");
        Guid anexo = await SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Docx, "quadro.docx");
        string chave = Guid.CreateVersion7().ToString("N");

        Guid atoId;
        using (HttpResponseMessage resposta = await PublicarAvisoAsync(client, processoId, principal, chave, anexos: [new { documentoId = anexo, nome = "Quadro de vagas" }]))
        {
            resposta.StatusCode.Should().Be(HttpStatusCode.Accepted, await resposta.Content.ReadAsStringAsync());
            atoId = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("atoId").GetGuid();
        }

        using (HttpResponseMessage repeticao = await PublicarAvisoAsync(client, processoId, principal, chave, anexos: [new { documentoId = anexo, nome = "Quadro de vagas" }]))
        {
            repeticao.StatusCode.Should().Be(HttpStatusCode.Accepted);
            (await repeticao.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("atoId").GetGuid().Should().Be(atoId);
        }

        await EsperaDeAtoRegistrado.AguardarAsync(api, atoId, "aviso avulso", processoId);
        await AguardarSituacaoAsync(api, atoId, SituacaoDoRegistroDoAto.Registrado);

        (await ContarAtosAsync(api, processoId, "AVISO")).Should().Be(1, "a repetição devolve a resposta guardada e não registra outro ato");

        JsonElement ato = (await ConsultarAtosAsync(client, processoId)).Single(a => a.GetProperty("id").GetGuid() == atoId);
        ato.GetProperty("situacao").GetString().Should().Be("registrado");
        ato.GetProperty("documentoPrincipal").GetProperty("documentoAPublicarId").GetGuid().Should().Be(principal);
        ato.GetProperty("anexos").EnumerateArray().Should().ContainSingle()
            .Which.GetProperty("nomeDeExibicao").GetString().Should().Be("Quadro de vagas.docx");

        JsonElement publicado = await AguardarNaPaginaPublicaAsync(client, processoId, atoId);
        JsonElement[] documentos = [.. publicado.GetProperty("documentos").EnumerateArray()];
        documentos.Select(d => d.GetProperty("papel").GetString()).Should().Equal("principal", "anexo");
        documentos[1].GetProperty("nome").GetString().Should().Be("Quadro de vagas.docx");
    }

    [Fact(DisplayName = "A errata que aponta o aviso é aceita e aparece com o motivo; a segunda errata do mesmo aviso é recusada, e a que aponta a errata é aceita")]
    public async Task Errata_CadeiaLinear()
    {
        (CascadingApiFactory api, HttpClient client) = await Preparar();
        (Guid processoId, _) = await PublicarEditalAsync(api, client);
        Guid aviso = await PublicarEAguardarAsync(api, client, processoId);

        Guid errata;
        using (HttpResponseMessage primeira = await PublicarAvisoAsync(
            client, processoId, await SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "errata.pdf"), atoCorrigidoId: aviso, motivo: "Data errada"))
        {
            primeira.StatusCode.Should().Be(HttpStatusCode.Accepted, await primeira.Content.ReadAsStringAsync());
            errata = (await primeira.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("atoId").GetGuid();
        }

        JsonElement naConsulta = (await ConsultarAtosAsync(client, processoId)).Single(a => a.GetProperty("id").GetGuid() == errata);
        naConsulta.GetProperty("atoCorrigidoId").GetGuid().Should().Be(aviso);
        naConsulta.GetProperty("motivoDaCorrecao").GetString().Should().Be("Data errada");

        // A primeira ainda pode estar pendente: quem enxerga a correção é o registro da Seleção.
        using (HttpResponseMessage segunda = await PublicarAvisoAsync(
            client, processoId, await SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "errata2.pdf"), atoCorrigidoId: aviso, motivo: "Outra"))
        {
            await ExigirProblemaAsync(segunda, "uniplus.selecao.ato_do_processo.ato_corrigido_ja_corrigido");
        }

        await EsperaDeAtoRegistrado.AguardarAsync(api, errata, "errata do aviso", processoId, aviso);
        using (HttpResponseMessage segundaDepois = await PublicarAvisoAsync(
            client, processoId, await SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "errata3.pdf"), atoCorrigidoId: aviso, motivo: "Outra"))
        {
            await ExigirProblemaAsync(segundaDepois, "uniplus.selecao.ato_do_processo.ato_corrigido_ja_corrigido");
        }

        using HttpResponseMessage daErrata = await PublicarAvisoAsync(
            client, processoId, await SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "errata4.pdf"), atoCorrigidoId: errata, motivo: "Corrige a errata");
        daErrata.StatusCode.Should().Be(HttpStatusCode.Accepted, "a correção seguinte aponta o ato mais recente da cadeia");
    }

    [Fact(DisplayName = "Duas correções simultâneas do mesmo ato: uma é aceita e a outra recusada, sem erro de servidor")]
    public async Task CorrecoesSimultaneas_UmaVence()
    {
        (CascadingApiFactory api, HttpClient client) = await Preparar();
        (Guid processoId, _) = await PublicarEditalAsync(api, client);
        Guid aviso = await PublicarEAguardarAsync(api, client, processoId);
        Guid[] documentos = [.. await Task.WhenAll(Enumerable.Range(0, 4).Select(i =>
            SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, $"errata{i}.pdf")))];

        HttpResponseMessage[] respostas = await Task.WhenAll(documentos.Select(d =>
            PublicarAvisoAsync(client, processoId, d, atoCorrigidoId: aviso, motivo: "Corrida")));

        respostas.Select(r => r.StatusCode).Count(s => s == HttpStatusCode.Accepted).Should().Be(1);
        respostas.Where(r => r.StatusCode != HttpStatusCode.Accepted).Should().OnlyContain(r => r.StatusCode == HttpStatusCode.UnprocessableEntity);
        foreach (HttpResponseMessage resposta in respostas.Where(r => r.StatusCode != HttpStatusCode.Accepted))
        {
            await ExigirProblemaAsync(resposta, "uniplus.selecao.ato_do_processo.ato_corrigido_ja_corrigido");
        }

        foreach (HttpResponseMessage resposta in respostas)
        {
            resposta.Dispose();
        }
    }

    [Fact(DisplayName = "Duas publicações simultâneas com o mesmo documento: uma é aceita e a outra recusada por documento já usado")]
    public async Task PublicacoesSimultaneas_MesmoDocumento_UmaVence()
    {
        (CascadingApiFactory api, HttpClient client) = await Preparar();
        (Guid processoId, _) = await PublicarEditalAsync(api, client);
        Guid documento = await SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "aviso.pdf");

        HttpResponseMessage[] respostas = await Task.WhenAll(
            PublicarAvisoAsync(client, processoId, documento),
            PublicarAvisoAsync(client, processoId, documento));

        await ExigirUmaAceitaEUmaRecusadaAsync(respostas, "uniplus.selecao.ato_do_processo.documento_ja_usado_em_outro_ato");
        (await ContarAtosAsync(api, processoId, DadosDoAtoDeTeste.TipoAviso)).Should().Be(1);
    }

    [Fact(DisplayName = "Duas publicações simultâneas do tipo único por objeto: uma é aceita e a outra recusada por vaga ocupada")]
    public async Task PublicacoesSimultaneas_TipoUnico_UmaVence()
    {
        (CascadingApiFactory api, HttpClient client) = await Preparar();
        (Guid processoId, _) = await PublicarEditalAsync(api, client);
        Guid[] documentos = [.. await Task.WhenAll(Enumerable.Range(0, 2).Select(i =>
            SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, $"comunicado{i}.pdf")))];

        HttpResponseMessage[] respostas = await Task.WhenAll(documentos.Select(d =>
            PublicarAvisoAsync(client, processoId, d, tipo: DadosDoAtoDeTeste.TipoAvulsoUnico)));

        await ExigirUmaAceitaEUmaRecusadaAsync(respostas, "uniplus.selecao.processo_seletivo.objeto_ja_tem_ato_vivo_do_tipo");
        (await ContarAtosAsync(api, processoId, DadosDoAtoDeTeste.TipoAvulsoUnico)).Should().Be(1);
    }

    [Fact(DisplayName = "O ato corrigido de outro processo seletivo é recusado")]
    public async Task AtoCorrigidoDeOutroProcesso_Recusa()
    {
        (CascadingApiFactory api, HttpClient client) = await Preparar();
        (Guid processoId, _) = await PublicarEditalAsync(api, client);
        (Guid outroProcessoId, _) = await PublicarEditalAsync(api, client);
        Guid avisoDoOutro = await PublicarEAguardarAsync(api, client, outroProcessoId);

        using HttpResponseMessage resposta = await PublicarAvisoAsync(
            client, processoId, await SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "errata.pdf"), atoCorrigidoId: avisoDoOutro, motivo: "Erro");

        await ExigirProblemaAsync(resposta, "uniplus.selecao.ato_do_processo.ato_corrigido_de_outro_processo");
    }

    [Fact(DisplayName = "A correção sem motivo é recusada com 422 e aponta o campo do motivo")]
    public async Task CorrecaoSemMotivo_Recusa()
    {
        (CascadingApiFactory api, HttpClient client) = await Preparar();
        (Guid processoId, Guid editalAtoId) = await PublicarEditalAsync(api, client);

        using HttpResponseMessage resposta = await PublicarAvisoAsync(
            client, processoId, await SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "errata.pdf"), atoCorrigidoId: editalAtoId);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        JsonElement problema = await resposta.Content.ReadFromJsonAsync<JsonElement>();
        problema.GetProperty("code").GetString().Should().Be("uniplus.selecao.ato_do_processo.motivo_da_correcao_obrigatorio");
        problema.GetProperty("errors")[0].GetProperty("field").GetString().Should().Be("motivoDaCorrecao");
    }

    [Fact(DisplayName = "O edital, que congela configuração, não é corrigido por documento avulso")]
    public async Task CorrigirOEdital_Recusa()
    {
        (CascadingApiFactory api, HttpClient client) = await Preparar();
        (Guid processoId, Guid editalAtoId) = await PublicarEditalAsync(api, client);

        using HttpResponseMessage resposta = await PublicarAvisoAsync(
            client, processoId, await SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "errata.pdf"), atoCorrigidoId: editalAtoId, motivo: "Erro");

        await ExigirProblemaAsync(resposta, "uniplus.selecao.ato_do_processo.ato_corrigido_de_outra_classe");
    }

    [Fact(DisplayName = "O documento já usado em outro ato do processo é recusado")]
    public async Task DocumentoJaUsado_Recusa()
    {
        (CascadingApiFactory api, HttpClient client) = await Preparar();
        (Guid processoId, _) = await PublicarEditalAsync(api, client);
        Guid documento = await SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "aviso.pdf");
        using (HttpResponseMessage primeiro = await PublicarAvisoAsync(client, processoId, documento))
        {
            primeiro.StatusCode.Should().Be(HttpStatusCode.Accepted);
        }

        using HttpResponseMessage segundo = await PublicarAvisoAsync(client, processoId, documento);

        await ExigirProblemaAsync(segundo, "uniplus.selecao.ato_do_processo.documento_ja_usado_em_outro_ato");
    }

    [Fact(DisplayName = "O tipo que congela configuração não é documento avulso")]
    public async Task TipoQueCongela_Recusa()
    {
        (CascadingApiFactory api, HttpClient client) = await Preparar();
        (Guid processoId, _) = await PublicarEditalAsync(api, client);

        using HttpResponseMessage resposta = await PublicarAvisoAsync(
            client, processoId, await SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "retificacao.pdf"), tipo: DadosDoAtoDeTeste.TipoRetificacao);

        await ExigirProblemaAsync(resposta, "uniplus.selecao.ato_do_processo.tipo_congela_configuracao");
    }

    [Fact(DisplayName = "O processo ainda sem página pública não recebe documento avulso")]
    public async Task ProcessoSemPaginaPublica_Recusa()
    {
        (CascadingApiFactory api, HttpClient client) = await Preparar();
        Guid processoId;
        await using (AsyncServiceScope escopo = api.Services.CreateAsyncScope())
        {
            (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
                escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>(), $"Sem página {Guid.CreateVersion7()}");
            processoId = processo.Id;
        }

        using HttpResponseMessage resposta = await PublicarAvisoAsync(
            client, processoId, await SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "aviso.pdf"));

        await ExigirProblemaAsync(resposta, "uniplus.selecao.ato_do_processo.certame_nao_divulgado");
    }

    private async Task<(CascadingApiFactory Api, HttpClient Client)> Preparar()
    {
        CascadingApiFactory api = _fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        return (api, api.CreateClient());
    }

    /// <summary>Publica o edital de abertura pela API e espera a divulgação do certame, que o ato avulso exige.</summary>
    internal static async Task<(Guid ProcessoId, Guid EditalAtoId)> PublicarEditalAsync(
        CascadingApiFactory api,
        HttpClient client,
        IReadOnlyList<ProdutoDaFase>? produtosDaFase = null)
    {
        Guid processoId;
        Guid documentoEdital;
        await using (AsyncServiceScope escopo = api.Services.CreateAsyncScope())
        {
            (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(
                escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>(), $"Atos avulsos {Guid.CreateVersion7()}", produtosDaFase: produtosDaFase);
            processoId = processo.Id;
            documentoEdital = documento.Id;
        }

        using HttpRequestMessage pedido = Autenticado(HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/publicacao");
        pedido.Content = JsonContent.Create(new
        {
            numero = "001/2026",
            documentoEditalId = documentoEdital,
            ato = new
            {
                orgao = "CEPS",
                serie = "EDITAL",
                ano = 2026,
                dataPublicacao = HojeEmTexto(),
                assinante = "Diretor do CEPS",
                tipoAtoCodigo = DadosDoAtoDeTeste.TipoAbertura,
            },
        });
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        (await client.SendAsync(pedido)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        Guid editalAtoId;
        await using (AsyncServiceScope escopo = api.Services.CreateAsyncScope())
        {
            editalAtoId = await escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>().VersoesConfiguracao.AsNoTracking()
                .Where(v => v.ProcessoSeletivoId == processoId)
                .Select(v => v.AtoCriadorId)
                .SingleAsync();
        }

        await EsperaDeAtoRegistrado.AguardarAsync(api, editalAtoId, "edital de abertura", processoId);
        await AguardarDivulgacaoAsync(api, processoId);
        return (processoId, editalAtoId);
    }

    private static async Task<Guid> PublicarEAguardarAsync(CascadingApiFactory api, HttpClient client, Guid processoId)
    {
        Guid documento = await SemearDocumentoAsync(api, processoId, TiposDeMidiaDeDocumento.Pdf, "aviso.pdf");
        using HttpResponseMessage resposta = await PublicarAvisoAsync(client, processoId, documento);
        resposta.StatusCode.Should().Be(HttpStatusCode.Accepted, await resposta.Content.ReadAsStringAsync());
        Guid atoId = (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("atoId").GetGuid();
        await EsperaDeAtoRegistrado.AguardarAsync(api, atoId, "aviso", processoId);
        await AguardarSituacaoAsync(api, atoId, SituacaoDoRegistroDoAto.Registrado);
        return atoId;
    }

    internal static Task<HttpResponseMessage> PublicarAvisoAsync(
        HttpClient client,
        Guid processoId,
        Guid documentoPrincipal,
        string? chave = null,
        object[]? anexos = null,
        Guid? atoCorrigidoId = null,
        string? motivo = null,
        string tipo = DadosDoAtoDeTeste.TipoAviso)
    {
        HttpRequestMessage pedido = Autenticado(HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/atos");
        pedido.Content = JsonContent.Create(new
        {
            tipoAtoCodigo = tipo,
            numero = "5/2026",
            dataPublicacao = HojeEmTexto(),
            assinante = "Diretor do CEPS",
            documentoPrincipalId = documentoPrincipal,
            anexos,
            atoCorrigidoId,
            motivoDaCorrecao = motivo,
        });
        pedido.Headers.TryAddWithoutValidation("Idempotency-Key", chave ?? Guid.CreateVersion7().ToString("N"));
        return client.SendAsync(pedido);
    }

    internal static async Task ExigirUmaAceitaEUmaRecusadaAsync(HttpResponseMessage[] respostas, string codigoDaRecusa)
    {
        try
        {
            respostas.Count(r => r.StatusCode == HttpStatusCode.Accepted).Should().Be(1);
            HttpResponseMessage recusada = respostas.Single(r => r.StatusCode != HttpStatusCode.Accepted);
            await ExigirProblemaAsync(recusada, codigoDaRecusa);
        }
        finally
        {
            foreach (HttpResponseMessage resposta in respostas)
            {
                resposta.Dispose();
            }
        }
    }

    internal static async Task ExigirProblemaAsync(HttpResponseMessage resposta, string codigo)
    {
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, await resposta.Content.ReadAsStringAsync());
        (await resposta.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString().Should().Be(codigo);
    }

    internal static HttpRequestMessage Autenticado(HttpMethod metodo, string rota)
    {
        HttpRequestMessage pedido = new(metodo, new Uri(rota, UriKind.Relative));
        pedido.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        pedido.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        return pedido;
    }

    internal static string HojeEmTexto() =>
        DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    internal static async Task<IReadOnlyList<JsonElement>> ConsultarAtosAsync(HttpClient client, Guid processoId)
    {
        using HttpRequestMessage pedido = Autenticado(HttpMethod.Get, $"/api/selecao/processos-seletivos/{processoId}/atos");
        pedido.Headers.TryAddWithoutValidation("Accept", "application/vnd.uniplus.ato-do-processo.v1+json");
        using HttpResponseMessage resposta = await client.SendAsync(pedido);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        return [.. (await resposta.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray().Select(static e => e.Clone())];
    }

    /// <summary>A divulgação chega pela fila depois do registro do ato, então a página pode demorar a existir.</summary>
    internal static async Task<JsonElement> AguardarNaPaginaPublicaAsync(HttpClient client, Guid processoId, Guid atoId)
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            using HttpResponseMessage resposta = await client.GetAsync(new Uri($"/api/selecao/certames/{processoId}", UriKind.Relative));
            if (resposta.StatusCode == HttpStatusCode.OK)
            {
                JsonElement certame = await resposta.Content.ReadFromJsonAsync<JsonElement>();
                foreach (JsonElement edital in certame.GetProperty("editaisPublicados").EnumerateArray())
                {
                    if (edital.GetProperty("atoId").GetGuid() == atoId)
                    {
                        return edital.Clone();
                    }
                }
            }

            DateTimeOffset.UtcNow.Should().BeBefore(limite, "o ato avulso não chegou à página pública do certame");
            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }
    }

    internal static async Task AguardarDivulgacaoAsync(CascadingApiFactory api, Guid processoId)
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
            if (await escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>().CertamesDivulgados.AsNoTracking()
                .AnyAsync(c => c.Id == processoId))
            {
                return;
            }

            DateTimeOffset.UtcNow.Should().BeBefore(limite, "a divulgação do certame não chegou depois do registro do edital");
            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }
    }

    private static async Task<int> ContarAtosAsync(CascadingApiFactory api, Guid processoId, string tipo)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>().AtosDoProcesso.AsNoTracking()
            .CountAsync(a => a.ProcessoSeletivoId == processoId && a.TipoCodigo == tipo);
    }

    internal static async Task AguardarSituacaoAsync(CascadingApiFactory api, Guid atoId, SituacaoDoRegistroDoAto esperada)
    {
        DateTimeOffset limite = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
            AtoDoProcesso ato = await escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>().AtosDoProcesso.AsNoTracking()
                .SingleAsync(a => a.Id == atoId);
            if (ato.Situacao == esperada || DateTimeOffset.UtcNow >= limite)
            {
                ato.Situacao.Should().Be(esperada, "a confirmação do Publicações chega pela fila durável");
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300));
        }
    }

    internal static async Task<Guid> SemearDocumentoAsync(CascadingApiFactory api, Guid processoId, string tipoDeMidia, string nome)
    {
        await using AsyncServiceScope escopo = api.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            processoId, FinalidadeDoDocumento.DocumentoDeAto, nome, tipoDeMidia, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(1024, new string('d', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        db.DocumentosAPublicar.Add(documento);
        await db.SaveChangesAsync();
        return documento.Id;
    }
}
