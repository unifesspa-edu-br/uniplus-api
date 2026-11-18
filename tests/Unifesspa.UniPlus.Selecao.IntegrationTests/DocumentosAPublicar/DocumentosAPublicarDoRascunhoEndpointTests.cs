namespace Unifesspa.UniPlus.Selecao.IntegrationTests.DocumentosAPublicar;

using System.Net;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;
using Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Wolverine;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Rascunhos.ClienteDoRascunhoDeTeste;

/// <summary>
/// O documento a publicar de um rascunho, que ainda não tem linha de processo, pela API: a lista e o acesso o alcançam
/// por quem publica na unidade do rascunho, o identificador desconhecido é inexistente, e excluir o rascunho leva os
/// documentos dele, o confirmado inclusive.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
public sealed class DocumentosAPublicarDoRascunhoEndpointTests : IAsyncLifetime
{
    private const string Leandro = "leandro.viana";
    private const string MidiaDaLista = "application/vnd.uniplus.documento-a-publicar.v1+json";
    private const string MidiaDoAcesso = "application/vnd.uniplus.acesso-documento-a-publicar.v1+json";

    private readonly CascadingFixture _fixture;
    private readonly ClienteDoRascunhoDeTeste _cliente;

    public DocumentosAPublicarDoRascunhoEndpointTests(CascadingFixture fixture)
    {
        _fixture = fixture;
        _cliente = new ClienteDoRascunhoDeTeste(fixture);
    }

    public async Task InitializeAsync()
    {
        await ConcessoesDeTeste.RemoverDaPessoaAsync(_fixture.Factory.Services, Leandro);
        Guid perfil = await ConcessoesDeTeste.CriarPerfilAsync(
            _fixture.Factory.Services, PermissaoSelecao.ProcessosConfigurar.Codigo, PermissaoSelecao.ProcessosPublicar.Codigo);
        await ConcessoesDeTeste.ConcederAsync(
            _fixture.Factory.Services, Leandro, perfil, EscopoDaConcessao.DaUnidade(UnidadesDeTeste.Ceps.OrigemId));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact(DisplayName = "A lista do rascunho responde 200 com o documento dele")]
    public async Task Listar_Rascunho_200()
    {
        Guid rascunhoId = await _cliente.CriarAsync(Leandro, "CEPS");
        DocumentoAPublicar confirmado = await SemearConfirmadoAsync(rascunhoId);

        using HttpResponseMessage resposta = await LerAsync($"{RotaDosDocumentos(rascunhoId)}", MidiaDaLista);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await Corpo(resposta)).EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().Equal(confirmado.Id);
    }

    [Fact(DisplayName = "O acesso ao documento que o rascunho não tem responde 404, e a lista do identificador desconhecido também")]
    public async Task DocumentoOuProcessoInexistente_404()
    {
        Guid rascunhoId = await _cliente.CriarAsync(Leandro, "CEPS");

        using HttpResponseMessage semDocumento = await LerAsync($"{RotaDosDocumentos(rascunhoId)}/{Guid.CreateVersion7()}/acesso", MidiaDoAcesso);
        using HttpResponseMessage semProcesso = await LerAsync(RotaDosDocumentos(Guid.CreateVersion7()), MidiaDaLista);

        semDocumento.StatusCode.Should().Be(HttpStatusCode.NotFound);
        semProcesso.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact(DisplayName = "Excluir o rascunho com documento confirmado responde 204 e apaga as linhas dele")]
    public async Task ExcluirRascunhoComDocumentoConfirmado_204ELinhasApagadas()
    {
        Guid rascunhoId = await _cliente.CriarAsync(Leandro, "CEPS");
        await SemearConfirmadoAsync(rascunhoId);

        using HttpResponseMessage excluido = await ExcluirAsync(rascunhoId, "\"1\"");

        excluido.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await DocumentosDoDonoAsync(rascunhoId)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A exclusão grava no outbox, na mesma transação, a remoção agendada com as duas chaves de cada documento")]
    public async Task ExcluirRascunho_GravaARemocaoAgendadaNoOutbox()
    {
        Guid rascunhoId = await _cliente.CriarAsync(Leandro, "CEPS");
        DocumentoAPublicar confirmado = await SemearAsync(DocumentoConfirmado(rascunhoId));
        DocumentoAPublicar pendente = await SemearAsync(EnviosPendentesDeTeste.Documento(rascunhoId));
        DateTimeOffset antes = DateTimeOffset.UtcNow;

        using HttpResponseMessage excluido = await ExcluirAsync(rascunhoId, "\"1\"");

        excluido.StatusCode.Should().Be(HttpStatusCode.NoContent);
        Envelope remocao = (await RemocoesPendentesDoRascunho.ListarAsync(_fixture.Factory.Services, rascunhoId)).Should().ContainSingle().Subject;
        ChavesDe(remocao).Should().BeEquivalentTo(
            [confirmado.ObjectKey, confirmado.ChaveDaCopiaGravada, pendente.ObjectKey, pendente.ChaveDaCopiaGravada]);
        remocao.ScheduledTime.Should().BeCloseTo(antes + RemoverArquivosDoRascunhoExcluido.Atraso, TimeSpan.FromMinutes(1));
    }

    [Fact(DisplayName = "A exclusão recusada pela precondição responde 412, mantém as linhas e não grava remoção no outbox")]
    public async Task ExcluirRascunhoComPrecondicaoDefasada_412SemRemocaoNoOutbox()
    {
        Guid rascunhoId = await _cliente.CriarAsync(Leandro, "CEPS");
        await SemearConfirmadoAsync(rascunhoId);

        using HttpResponseMessage recusado = await ExcluirAsync(rascunhoId, "\"4\"");

        recusado.StatusCode.Should().Be(HttpStatusCode.PreconditionFailed);
        (await DocumentosDoDonoAsync(rascunhoId)).Should().ContainSingle();
        (await RemocoesPendentesDoRascunho.ListarAsync(_fixture.Factory.Services, rascunhoId)).Should().BeEmpty();
        using HttpResponseMessage rascunhoIntacto = await _cliente.EnviarAsync(Leandro, HttpMethod.Get, $"{Rota}/{rascunhoId}");
        rascunhoIntacto.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static string RotaDosDocumentos(Guid donoId) => $"/api/selecao/processos-seletivos/{donoId}/documentos-a-publicar";

    private async Task<HttpResponseMessage> LerAsync(string rota, string midia)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage requisicao = Requisicao(Leandro, HttpMethod.Get, rota);
        requisicao.Headers.TryAddWithoutValidation("Accept", midia);
        return await client.SendAsync(requisicao);
    }

    private static IEnumerable<string> ChavesDe(Envelope remocao) =>
        JsonSerializer.Deserialize<RemoverArquivosDoRascunhoExcluido>(remocao.Data!, JsonSerializerOptions.Web)!.Chaves;

    private static DocumentoAPublicar DocumentoConfirmado(Guid donoId)
    {
        DocumentoAPublicar documento = EnviosPendentesDeTeste.Documento(donoId);
        documento.Confirmar(1024, new string('a', 64), TimeProvider.System).IsSuccess.Should().BeTrue();
        return documento;
    }

    private Task<HttpResponseMessage> ExcluirAsync(Guid rascunhoId, string ifMatch) =>
        _cliente.EnviarAsync(Leandro, HttpMethod.Delete, $"{Rota}/{rascunhoId}", ifMatch: ifMatch);

    private Task<DocumentoAPublicar> SemearConfirmadoAsync(Guid donoId) => SemearAsync(DocumentoConfirmado(donoId));

    private async Task<DocumentoAPublicar> SemearAsync(DocumentoAPublicar documento)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        contexto.DocumentosAPublicar.Add(documento);
        await contexto.SaveChangesAsync();
        return documento;
    }

    private async Task<IReadOnlyList<DocumentoAPublicar>> DocumentosDoDonoAsync(Guid donoId)
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext contexto = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await contexto.DocumentosAPublicar.AsNoTracking().Where(d => d.ProcessoSeletivoId == donoId).ToListAsync();
    }
}
