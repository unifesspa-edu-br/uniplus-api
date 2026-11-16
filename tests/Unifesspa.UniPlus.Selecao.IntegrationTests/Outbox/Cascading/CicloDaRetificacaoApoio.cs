namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>Cenário e leituras que os testes do ciclo da retificação compartilham.</summary>
internal static class CicloDaRetificacaoApoio
{
    internal static async Task<CicloDaRetificacao> PublicarAsync(CascadingFixture fixture, string nome)
    {
        CascadingApiFactory api = fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        HttpClient client = api.CreateClient();

        Guid processoId;
        Guid documentoId;
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
            (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder
                .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
            processoId = processo.Id;
            documentoId = documento.Id;
        }

        CicloDaRetificacao c = new(api, client, processoId);

        using HttpRequestMessage publicar = new(
            HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative))
        {
            Content = JsonContent.Create(new
            {
                numero = "001/2026",
                documentoEditalId = documentoId,
                ato = new
                {
                    orgao = "CEPS",
                    serie = "EDITAL",
                    ano = 2026,
                    dataDoAto = Hoje(),
                    assinante = "Diretor do CEPS",
                    tipoAtoCodigo = "EDITAL_ABERTURA",
                },
            }),
        };
        publicar.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        publicar.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        publicar.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));

        (await client.SendAsync(publicar)).StatusCode.Should().Be(
            HttpStatusCode.NoContent, "o cenário depende de um certame publicado");

        return c;
    }

    /// <summary>A configuração VIVA — o que o administrador está editando.</summary>
    internal static async Task<decimal> LerPesoDaEtapaVivaAsync(CicloDaRetificacao c)
    {
        HttpResponseMessage r = await c.ObterRecursoAsync();
        r.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("etapas")[0].GetProperty("nota").GetProperty("peso").GetDecimal();
    }

    /// <summary>A versão CONGELADA — o que o mundo vê.</summary>
    internal static async Task<(string Hash, string Bytes)> LerSnapshotVigenteAsync(CicloDaRetificacao c)
    {
        HttpResponseMessage r = await c.ObterSnapshotVigenteAsync();
        r.StatusCode.Should().Be(HttpStatusCode.OK);

        using JsonDocument doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        JsonElement raiz = doc.RootElement;
        return (
            raiz.GetProperty("hashConfiguracao").GetString()!,
            raiz.GetProperty("configuracao").GetRawText());
    }

    /// <summary>
    /// Anula só o identificador vivo da raiz, deixando a versão vigente com o valor que ela congelou:
    /// é a divergência que prova que a sessão decide pelo envelope, e não pela raiz.
    /// </summary>
    internal static async Task RetirarIdentificadorDaRaizAsync(CicloDaRetificacao c)
    {
        await using AsyncServiceScope scope = c.Api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        int alterados = await db.ProcessosSeletivos
            .Where(p => p.Id == c.ProcessoId)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.IdentificadorLegivel, (Domain.ValueObjects.IdentificadorLegivel?)null));
        alterados.Should().Be(1);
    }

    internal static async Task<string?> LerIdentificadorVivoAsync(CicloDaRetificacao c)
    {
        await using AsyncServiceScope scope = c.Api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.AsNoTracking().SingleAsync(p => p.Id == c.ProcessoId);
        return processo.IdentificadorLegivel?.Valor;
    }

    internal static async Task<int> ContarVersoesAsync(CicloDaRetificacao c)
    {
        await using AsyncServiceScope scope = c.Api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await db.VersoesConfiguracao.AsNoTracking().CountAsync(v => v.ProcessoSeletivoId == c.ProcessoId);
    }

    internal static async Task<int> ContarRascunhosAsync(CicloDaRetificacao c)
    {
        await using AsyncServiceScope scope = c.Api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await db.RascunhosRetificacao.AsNoTracking().CountAsync(r => r.ProcessoSeletivoId == c.ProcessoId);
    }

    internal static async Task<Guid> SemearDocumentoConfirmadoAsync(CicloDaRetificacao c)
    {
        await using AsyncServiceScope scope = c.Api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        string hash = string.Concat(Enumerable.Repeat("cd45670189", 7))[..64];
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(c.ProcessoId, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(2048, hash, TimeProvider.System).IsSuccess.Should().BeTrue();
        await db.DocumentosAPublicar.AddAsync(documento);
        await db.SaveChangesAsync();
        return documento.Id;
    }

    internal static string LerETag(HttpResponseMessage r)
    {
        r.Headers.ETag.Should().NotBeNull($"a resposta {(int)r.StatusCode} de uma sessão editorial carrega o ETag");
        return r.Headers.ETag!.ToString();
    }

    internal static string Hoje() =>
        DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
