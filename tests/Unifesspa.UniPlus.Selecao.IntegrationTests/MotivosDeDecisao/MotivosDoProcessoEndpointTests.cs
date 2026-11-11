namespace Unifesspa.UniPlus.Selecao.IntegrationTests.MotivosDeDecisao;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Outbox.Cascading;

using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// A escolha dos motivos pelo processo pelo endpoint real, contra Postgres via Testcontainers
/// (UNI-REQ-0122, UNI-REQ-0285): a cópia por valor, a recusa da publicação com motivo desativado
/// depois de escolhido, o congelamento na publicação e a recusa de retirar o publicado.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class MotivosDoProcessoEndpointTests : IAsyncLifetime
{
    private readonly CascadingFixture _fixture;

    public MotivosDoProcessoEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);

    public async Task DisposeAsync()
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        await db.Database.ExecuteSqlAsync($"DELETE FROM selecao.motivos_do_processo WHERE codigo LIKE 'TEST\\_MDP\\_%' ESCAPE '\\'");
        await db.Database.ExecuteSqlAsync($"DELETE FROM selecao.motivos_de_decisao WHERE codigo LIKE 'TEST\\_MDP\\_%' ESCAPE '\\'");
    }

    [Fact(DisplayName = "No rascunho, o processo inclui o motivo e a leitura devolve a cópia ainda não congelada")]
    public async Task Rascunho_Inclui_ListaACopia()
    {
        (Guid processoId, _) = await SemearProcessoAsync(nameof(Rascunho_Inclui_ListaACopia));
        MotivoDeDecisao motivo = await SemearMotivoAsync();
        using HttpClient client = Cliente();

        using HttpResponseMessage inclusao = await IncluirAsync(client, processoId, motivo.Id);
        inclusao.StatusCode.Should().Be(HttpStatusCode.NoContent, await inclusao.Content.ReadAsStringAsync());

        JsonElement escolhido = (await ListarAsync(client, processoId)).Should().ContainSingle().Subject;
        escolhido.GetProperty("codigo").GetString().Should().Be(motivo.Codigo.Valor);
        escolhido.GetProperty("tipo").GetString().Should().Be("HOMOLOGACAO_DAS_INSCRICOES");
        escolhido.GetProperty("congelado").GetBoolean().Should().BeFalse();
    }

    [Fact(DisplayName = "Incluir motivo desativado é recusado com o código nomeado")]
    public async Task Incluir_MotivoDesativado_Recusa()
    {
        (Guid processoId, _) = await SemearProcessoAsync(nameof(Incluir_MotivoDesativado_Recusa));
        MotivoDeDecisao motivo = await SemearMotivoAsync(ativo: false);
        using HttpClient client = Cliente();

        using HttpResponseMessage resposta = await IncluirAsync(client, processoId, motivo.Id);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoDoErro(resposta)).Should().Be("uniplus.selecao.motivo_de_decisao.motivo_inativo");
    }

    [Fact(DisplayName = "Publicar com motivo desativado depois de escolhido é recusado; trocado, publica e congela a cópia")]
    public async Task Publicar_MotivoDesativadoDepois_RecusaAteATroca()
    {
        (Guid processoId, Guid documentoId) = await SemearProcessoAsync(nameof(Publicar_MotivoDesativadoDepois_RecusaAteATroca));
        MotivoDeDecisao desativadoDepois = await SemearMotivoAsync();
        MotivoDeDecisao substituto = await SemearMotivoAsync();
        using HttpClient client = Cliente();
        (await IncluirAsync(client, processoId, desativadoDepois.Id)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AlterarMotivoAsync(desativadoDepois.Id, static m => m.Desativar());

        using HttpResponseMessage recusada = await PublicarAsync(client, processoId, documentoId);
        recusada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await CodigoDoErro(recusada)).Should().Be("uniplus.selecao.motivo_de_decisao.motivo_desativado");

        (await RetirarAsync(client, processoId, desativadoDepois.Id)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await IncluirAsync(client, processoId, substituto.Id)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        await AlterarMotivoAsync(substituto.Id, static m => m.Atualizar("Redação vigente na publicação."));

        using HttpResponseMessage publicada = await PublicarAsync(client, processoId, documentoId);
        publicada.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicada.Content.ReadAsStringAsync());

        await AlterarMotivoAsync(substituto.Id, static m => m.Atualizar("Redação posterior à publicação."));
        JsonElement congelado = (await ListarAsync(client, processoId)).Should().ContainSingle().Subject;
        congelado.GetProperty("congelado").GetBoolean().Should().BeTrue();
        congelado.GetProperty("descricao").GetString().Should().Be("Redação vigente na publicação.",
            "a cópia congela na publicação: mudar o cadastro depois não muda o processo");
    }

    [Fact(DisplayName = "Depois da publicação, sem retificação, o processo não retira nem inclui motivo")]
    public async Task Publicado_SemRetificacao_Recusa()
    {
        (Guid processoId, Guid documentoId) = await SemearProcessoAsync(nameof(Publicado_SemRetificacao_Recusa));
        MotivoDeDecisao publicado = await SemearMotivoAsync();
        MotivoDeDecisao novo = await SemearMotivoAsync();
        using HttpClient client = Cliente();
        (await IncluirAsync(client, processoId, publicado.Id)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await PublicarAsync(client, processoId, documentoId)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpResponseMessage retirada = await RetirarAsync(client, processoId, publicado.Id);
        using HttpResponseMessage inclusao = await IncluirAsync(client, processoId, novo.Id);

        retirada.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        inclusao.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ListarAsync(client, processoId)).Should().ContainSingle();
    }

    private async Task<(Guid ProcessoId, Guid DocumentoId)> SemearProcessoAsync(string nome)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await ProcessoSeletivoPublicavelSeeder
            .SemearAsync(db, $"{nome} {Guid.CreateVersion7()}");
        return (processo.Id, documento.Id);
    }

    private async Task<MotivoDeDecisao> SemearMotivoAsync(bool ativo = true)
    {
        MotivoDeDecisao motivo = MotivoDeDecisao.Criar(new CamposDoMotivo(
            TipoMotivoDeDecisao.HomologacaoDasInscricoes,
            $"TEST_MDP_{Guid.NewGuid():N}"[..40].ToUpperInvariant(),
            "Sem resultado no retorno do INEP para a edição indicada",
            null,
            SentidoDoMotivo.Desfavoravel)).Value!;
        if (!ativo)
        {
            motivo.Desativar();
        }

        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        db.MotivosDeDecisao.Add(motivo);
        await db.SaveChangesAsync();
        return motivo;
    }

    private async Task AlterarMotivoAsync(Guid id, Func<MotivoDeDecisao, Kernel.Results.Result> alteracao)
    {
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        MotivoDeDecisao motivo = await db.MotivosDeDecisao.SingleAsync(m => m.Id == id);
        alteracao(motivo).IsSuccess.Should().BeTrue();
        await db.SaveChangesAsync();
    }

    private HttpClient Cliente() => _fixture.Factory.CreateClient();

    private static Task<HttpResponseMessage> IncluirAsync(HttpClient client, Guid processoId, Guid motivoId) =>
        EnviarAsync(client, HttpMethod.Put, $"/api/selecao/processos-seletivos/{processoId}/motivos-de-decisao/{motivoId}", new { justificativa = (string?)null });

    private static Task<HttpResponseMessage> RetirarAsync(HttpClient client, Guid processoId, Guid motivoId) =>
        EnviarAsync(client, HttpMethod.Delete, $"/api/selecao/processos-seletivos/{processoId}/motivos-de-decisao/{motivoId}", corpo: null);

    private static Task<HttpResponseMessage> PublicarAsync(HttpClient client, Guid processoId, Guid documentoId) =>
        EnviarAsync(client, HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/publicacao", new
        {
            numero = (string?)null,
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

    private static async Task<IReadOnlyList<JsonElement>> ListarAsync(HttpClient client, Guid processoId)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri($"/api/selecao/processos-seletivos/{processoId}/motivos-de-decisao", UriKind.Relative));
        Autenticar(request);
        using HttpResponseMessage resposta = await client.SendAsync(request);
        resposta.StatusCode.Should().Be(HttpStatusCode.OK, await resposta.Content.ReadAsStringAsync());
        return [.. (await resposta.Content.ReadFromJsonAsync<JsonElement>()).EnumerateArray()];
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, HttpMethod metodo, string url, object? corpo)
    {
        using HttpRequestMessage request = new(metodo, new Uri(url, UriKind.Relative));
        if (corpo is not null)
        {
            request.Content = JsonContent.Create(corpo);
        }

        Autenticar(request);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }

    private static async Task<string?> CodigoDoErro(HttpResponseMessage resposta)
    {
        using JsonDocument doc = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("code").GetString();
    }

    private static void Autenticar(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
    }
}
