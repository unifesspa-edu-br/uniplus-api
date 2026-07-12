namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Domain.Entities;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

using Xunit.Abstractions;

/// <summary>
/// SPIKE #820 — diagnóstico: onde a requisição do ato para. Não é um teste de
/// propriedade; é um instrumento. Publica e depois lê as tabelas do Wolverine para dizer
/// se o envelope foi sequer gravado, se foi entregue, e se morreu na dead letter.
/// </summary>
[Collection(CascadingCollection.Name)]
public sealed class DiagnosticoFilaSpikeTests
{
    private readonly CascadingFixture _fixture;
    private readonly ITestOutputHelper _output;

    public DiagnosticoFilaSpikeTests(CascadingFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact(DisplayName = "SPIKE 820 · diagnóstico — onde para a requisição do ato")]
    public async Task Diagnosticar()
    {
        CascadingApiFactory api = _fixture.Factory;
        using HttpClient client = api.CreateClient();

        await SemearTipoAsync(api);

        Guid processoId;
        Guid documentoId;
        await using (AsyncServiceScope scope = api.Services.CreateAsyncScope())
        {
            SelecaoDbContext db = scope.ServiceProvider.GetRequiredService<SelecaoDbContext>();
            (ProcessoSeletivo processo, DocumentoEdital documento) = await ProcessoSeletivoPublicavelSeeder
                .SemearAsync(db, $"Diag {Guid.CreateVersion7()}");
            processoId = processo.Id;
            documentoId = documento.Id;
        }

        object corpo = new
        {
            numero = "001/2026",
            periodoInscricaoInicio = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            periodoInscricaoFim = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            documentoEditalId = documentoId,
        };
        using HttpRequestMessage request = new(HttpMethod.Post,
            new Uri($"/api/selecao/processos-seletivos/{processoId}/publicacao", UriKind.Relative))
        {
            Content = JsonContent.Create(corpo),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));

        HttpResponseMessage resposta = await client.SendAsync(request);
        _output.WriteLine($"POST publicacao => {(int)resposta.StatusCode}");

        await Task.Delay(TimeSpan.FromSeconds(10));

        await Despejar("SELECT count(*) FROM selecao.editais");
        await Despejar("SELECT count(*) FROM publicacoes.ato_normativo");
        await Despejar("SELECT message_type, status, attempts FROM wolverine.wolverine_incoming_envelopes");
        await Despejar("SELECT message_type, destination FROM wolverine.wolverine_outgoing_envelopes");
        await Despejar("SELECT message_type, exception_type, exception_message FROM wolverine.wolverine_dead_letters");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security", "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "SPIKE: SQL literal de diagnóstico, sem entrada de usuário.")]
    private async Task Despejar(string sql)
    {
        await using NpgsqlConnection conn = new(_fixture.ConnectionString);
        await conn.OpenAsync();
        await using NpgsqlCommand cmd = new(sql, conn);
        StringBuilder sb = new();
        sb.AppendLine(CultureInfo.InvariantCulture, $"--- {sql}");
        try
        {
            await using NpgsqlDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                object?[] valores = new object?[reader.FieldCount];
                reader.GetValues(valores!);
                sb.AppendLine(string.Join(" | ", valores.Select(v => v?.ToString() ?? "null")));
            }
        }
        catch (PostgresException ex)
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"ERRO: {ex.MessageText}");
        }

        _output.WriteLine(sb.ToString());
    }

    private static async Task SemearTipoAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        PublicacoesDbContext db = scope.ServiceProvider.GetRequiredService<PublicacoesDbContext>();
        if (await db.Set<TipoAtoPublicado>().AnyAsync(t => t.Codigo == "EDITAL_ABERTURA"))
        {
            return;
        }

        TipoAtoPublicado tipo = TipoAtoPublicado.Criar(
            "EDITAL_ABERTURA", "Edital de abertura", congelaConfiguracao: true, unicoPorObjeto: true,
            efeitoIrreversivel: false, new DateOnly(2020, 1, 1), null, null).Value!;
        await db.Set<TipoAtoPublicado>().AddAsync(tipo);
        await db.SaveChangesAsync();
    }
}
