namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

/// <summary>
/// O processo semeado, o cliente HTTP e as chamadas ao endpoint de formulários por finalidade,
/// compartilhados pelas classes de teste do formulário.
/// </summary>
internal static class ApoioDoFormularioPeloEndpoint
{
    internal enum Autenticacao
    {
        PlataformaAdmin,
        Nenhuma,
    }

    internal sealed record Contexto(CascadingApiFactory Api, HttpClient Client, Guid ProcessoId, Guid DocumentoId)
    {
        /// <summary>
        /// Espera a linha de divulgação aparecer. É ela que torna o certame público, e o formulário
        /// resolve por ela — logo, antes dela, não há formulário a servir.
        /// </summary>
        public async Task EsperarDivulgacaoAsync()
        {
            DateTimeOffset limite = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(30);
            while (DateTimeOffset.UtcNow < limite)
            {
                if (await DivulgacaoChegouAsync().ConfigureAwait(false))
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(300));
            }

            throw new InvalidOperationException(
                $"A divulgação do processo {ProcessoId} não chegou em 30s — sem ela o formulário não é servido.");
        }

        private async Task<bool> DivulgacaoChegouAsync()
        {
            await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
            UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
            return await db.CertamesDivulgados.AsNoTracking().AnyAsync(c => c.Id == ProcessoId).ConfigureAwait(false);
        }

        /// <summary>
        /// Publica o certame e espera a divulgação. O formulário segue a divulgação, que chega pela fila durável — o 204 da
        /// publicação volta muito antes dela existir. Sem esta espera, o GET corre contra a materialização e o teste mede a
        /// corrida, não o contrato.
        /// </summary>
        public async Task PublicarEEsperarDivulgacaoAsync()
        {
            await PublicarAsync().ConfigureAwait(false);
            await EsperarDivulgacaoAsync().ConfigureAwait(false);
        }

        public async Task<Guid> FaseDeInscricaoAsync()
        {
            await using AsyncServiceScope scope = Api.Services.CreateAsyncScope();
            UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
            ProcessoSeletivo processo = await db.ProcessosSeletivos.AsNoTracking().Include(static p => p.CronogramaFases)
                .SingleAsync(p => p.Id == ProcessoId).ConfigureAwait(false);
            return processo.CronogramaFases.Single(static f => f.ColetaInscricao).Id;
        }

        public async Task<HttpResponseMessage> PutFormularioAsync(
            string? titulo,
            Autenticacao autenticar = Autenticacao.PlataformaAdmin,
            string finalidade = "INSCRICAO",
            Guid? faseId = null)
        {
            Guid? fase = faseId ?? (finalidade == "INSCRICAO" ? await FaseDeInscricaoAsync().ConfigureAwait(false) : null);
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                new Uri($"/api/selecao/admin/processos-seletivos/{ProcessoId}/formularios/{finalidade}", UriKind.Relative))
            {
                Content = JsonContent.Create(new { faseId = fase, titulo, paginas = PaginasDoFormulario() }),
            };
            Autenticar(request, autenticar);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> GetRascunhoAsync(Autenticacao autenticar, string finalidade = "INSCRICAO")
        {
            using HttpRequestMessage request = new(
                HttpMethod.Get,
                new Uri($"/api/selecao/admin/processos-seletivos/{ProcessoId}/formularios/{finalidade}/apresentacao", UriKind.Relative));
            Autenticar(request, autenticar);
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> GetFormularioAsync(string finalidade = "INSCRICAO") => await Client.GetAsync(
            new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/formularios/{finalidade}", UriKind.Relative)).ConfigureAwait(false);

        public async Task<HttpResponseMessage> PutFatosAsync(IReadOnlyList<object> corpo)
        {
            using HttpRequestMessage request = new(
                HttpMethod.Put,
                FormularioDeInscricaoHttp.RotaDosItens(ProcessoId))
            {
                Content = JsonContent.Create(FormularioDeInscricaoHttp.CorpoDosItens(corpo)),
            };
            Autenticar(request, Autenticacao.PlataformaAdmin);
            request.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            return await Client.SendAsync(request).ConfigureAwait(false);
        }

        public async Task PublicarAsync()
        {
            using HttpRequestMessage publicar = new(
                HttpMethod.Post,
                new Uri($"/api/selecao/processos-seletivos/{ProcessoId}/publicacao", UriKind.Relative))
            {
                Content = JsonContent.Create(new { numero = "001/2026", documentoEditalId = DocumentoId, ato = AtoDaPublicacao() }),
            };
            Autenticar(publicar, Autenticacao.PlataformaAdmin);
            publicar.Headers.TryAddWithoutValidation("Idempotency-Key", MakeIdempotencyKey());
            HttpResponseMessage resposta = await Client.SendAsync(publicar).ConfigureAwait(false);
            resposta.StatusCode.Should().Be(HttpStatusCode.NoContent, "o cenário depende de um certame publicado");
        }
    }

    internal static async Task<Contexto> SemearRascunhoAsync(CascadingFixture fixture, string nome)
    {
        CascadingApiFactory api = fixture.Factory;
        await TiposDeAtoSeeder.SemearAsync(api.Services);
        (Guid processoId, Guid documentoId) = await SemearProcessoPublicavelAsync(api, nome);
        return new Contexto(api, api.CreateClient(), processoId, documentoId);
    }

    private static async Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoPublicavelAsync(CascadingApiFactory api, string nome)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"{nome} {Guid.CreateVersion7()}" });
        return (processo.Id, documento.Id);
    }

    internal static void Autenticar(HttpRequestMessage request, Autenticacao autenticacao)
    {
        if (autenticacao == Autenticacao.Nenhuma)
        {
            return;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(
            TestAuthHandler.RolesHeader,
            "plataforma-admin");
    }

    internal static object AtoDaPublicacao() => new
    {
        orgao = "CEPS",
        serie = "EDITAL",
        ano = 2026,
        dataDoAto = Hoje(),
        assinante = "Diretor do CEPS",
        tipoAtoCodigo = "EDITAL_ABERTURA",
    };

    internal static object[] PaginasDoFormulario() =>
    [
        new { codigo = "DADOS", ordem = 0, tipo = "SECAO", bloco = (string?)null, apresentacao = new { titulo = "Dados" } },
        new { codigo = "REVISAO", ordem = 1, tipo = "BLOCO", bloco = "REVISAO_E_ACEITE", apresentacao = new { titulo = "Revisão e aceite" } },
    ];

    private static string Hoje() =>
        DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    internal static string MakeIdempotencyKey() => Guid.CreateVersion7().ToString("N");
}
