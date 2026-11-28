namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;
using Domain.Enums;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O pedido de publicação ou de retificação do edital com o documento ainda não confirmado e o tipo do ato que o catálogo não tem, e a conferência de
/// que a resposta traz as duas recusas, cada uma no seu campo, com o código da resposta igual ao do documento.
/// </summary>
internal static class ApoioDasRecusasDoDocumentoEDoTipoNoEdital
{
    private const string CodigoDoDocumento = "uniplus.selecao.processo_seletivo.documento_nao_confirmado";
    private const string CodigoDoTipo = "uniplus.selecao.processo_seletivo.tipo_de_ato_sem_versao_vigente";

    public static object CorpoDaPublicacao(Guid documentoEditalId) => new
    {
        numero = "001/2026",
        documentoEditalId,
        ato = AtoComTipoInexistente(),
    };

    public static object CorpoDaRetificacao(Guid documentoEditalId) => new
    {
        motivo = "Correção do prazo de inscrição",
        numero = "001/2026-R1",
        documentoEditalId,
        ato = AtoComTipoInexistente(),
    };

    public static async Task<Guid> SemearDocumentoPendenteAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        FakeTimeProvider relogio = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(
            processoId, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, relogio, TimeSpan.FromMinutes(15)).Value!;
        await db.DocumentosAPublicar.AddAsync(documento);
        await db.SaveChangesAsync();

        return documento.Id;
    }

    public static async Task<int> ContarVersoesAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        return await db.VersoesConfiguracao.AsNoTracking().CountAsync(v => v.ProcessoSeletivoId == processoId);
    }

    /// <summary>422 com a recusa do documento e a do tipo, nessa ordem, cada uma com o campo (a chave nunca é nula) e o código da resposta é o do documento.</summary>
    public static async Task ConferirAsync(HttpResponseMessage resposta)
    {
        ArgumentNullException.ThrowIfNull(resposta);

        string corpo = await resposta.Content.ReadAsStringAsync();
        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, corpo);
        using JsonDocument problema = JsonDocument.Parse(corpo);
        problema.RootElement.GetProperty("code").GetString().Should().Be(CodigoDoDocumento);
        JsonElement[] erros = [.. problema.RootElement.GetProperty("errors").EnumerateArray()];
        erros.Should().HaveCount(2);
        erros.All(static e => e.TryGetProperty("field", out JsonElement campo) && campo.ValueKind == JsonValueKind.String)
            .Should().BeTrue("toda recusa traz o campo, e a chave nunca é nula");
        erros.Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString())).Should().Equal(
            ("documentoEditalId", CodigoDoDocumento), ("ato.tipoAtoCodigo", CodigoDoTipo));
    }

    /// <summary>404 com o código de processo não encontrado: a recusa de acesso vem antes de qualquer conferência do pedido.</summary>
    public static async Task ConferirProcessoNaoEncontradoAsync(HttpResponseMessage resposta)
    {
        ArgumentNullException.ThrowIfNull(resposta);

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.selecao.processo_seletivo.nao_encontrado");
        problema.RootElement.TryGetProperty("errors", out _).Should().BeFalse("o 404 não revela nenhuma das recusas do pedido");
    }

    private static object AtoComTipoInexistente() => new
    {
        orgao = "CEPS",
        serie = "EDITAL",
        ano = 2026,
        dataDoAto = "2026-01-01",
        assinante = "Diretor do CEPS",
        tipoAtoCodigo = "TIPO_QUE_NAO_EXISTE",
    };
}
