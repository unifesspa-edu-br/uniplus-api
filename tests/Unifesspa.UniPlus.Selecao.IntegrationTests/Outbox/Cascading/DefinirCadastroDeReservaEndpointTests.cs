namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

[Collection(CascadingCollection.Name)]
[Trait("Category", "Integration")]
public sealed class DefinirCadastroDeReservaEndpointTests(CascadingFixture fixture)
{
    private const string CorpoDoCadastroAnterior = """{ "chamadas": [{ "numeroDaChamada": 1, "pessoasDaReservaPorVaga": 2, "participacaoDaReserva": "ASSUME_NA_MESMA_CHAMADA" }, { "numeroDaChamada": 2, "pessoasDaReservaPorVaga": 0, "participacaoDaReserva": null }] }""";

    private static readonly (int Chamada, int Pessoas, ParticipacaoDaReserva? Participacao)[] CadastroAnterior =
        [(1, 2, ParticipacaoDaReserva.AssumeNaMesmaChamada), (2, 0, null)];

    [Fact(DisplayName = "PUT com as chamadas válidas responde 204 e grava o cadastro, com a chamada sem reserva incluída")]
    public async Task Put_ChamadasValidas_Retorna204EGrava()
    {
        Guid id = await SemearAsync();

        using HttpResponseMessage resposta = await EnviarAsync(id, """{ "chamadas": [{ "numeroDaChamada": 2, "pessoasDaReservaPorVaga": 3, "participacaoDaReserva": "ANTECIPA_A_HABILITACAO" }, { "numeroDaChamada": 1, "pessoasDaReservaPorVaga": 0, "participacaoDaReserva": null }] }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerGravadoAsync(id)).Should().Equal((1, 0, null), (2, 3, ParticipacaoDaReserva.AntecipaAHabilitacao));
    }

    [Fact(DisplayName = "PUT com a lista vazia responde 204 e deixa o processo sem reserva")]
    public async Task Put_ListaVazia_Retorna204ELimpa()
    {
        Guid id = await SemearAsync();
        (await EnviarAsync(id, """{ "chamadas": [{ "numeroDaChamada": 1, "pessoasDaReservaPorVaga": 2, "participacaoDaReserva": "ASSUME_NA_MESMA_CHAMADA" }] }""")).StatusCode.Should().Be(HttpStatusCode.NoContent);

        using HttpResponseMessage resposta = await EnviarAsync(id, """{ "chamadas": [] }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerGravadoAsync(id)).Should().BeEmpty();
    }

    [Fact(DisplayName = "PUT recusado devolve 422 com todas as causas por campo e não altera o cadastro")]
    public async Task Put_ChamadaInvalida_Retorna422ComTodasAsCausas()
    {
        Guid id = await SemearComCadastroAsync();

        using HttpResponseMessage resposta = await EnviarAsync(id, """{ "chamadas": [{ "numeroDaChamada": 1, "pessoasDaReservaPorVaga": 2, "participacaoDaReserva": "ASSUME_NA_MESMA_CHAMADA" }, { "numeroDaChamada": 1, "pessoasDaReservaPorVaga": -4, "participacaoDaReserva": null }, { "numeroDaChamada": 0, "pessoasDaReservaPorVaga": 1, "participacaoDaReserva": "ASSUME_NA_MESMA_CHAMADA" }] }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString())).Should().Equal(
                ("cadastroDeReserva[1].numeroDaChamada", "uniplus.selecao.cadastro_de_reserva.chamada_repetida"),
                ("cadastroDeReserva[1].pessoasDaReservaPorVaga", "uniplus.selecao.cadastro_de_reserva.pessoas_da_reserva_por_vaga_invalida"),
                ("cadastroDeReserva[2].numeroDaChamada", "uniplus.selecao.cadastro_de_reserva.numero_da_chamada_invalido"));
        (await LerGravadoAsync(id)).Should().Equal(CadastroAnterior);
    }

    [Fact(DisplayName = "PUT com participação ausente, com quantidade zero ou desconhecida devolve 422 com o campo da participação")]
    public async Task Put_ParticipacaoIncoerente_Retorna422ComOCampoDaParticipacao()
    {
        Guid id = await SemearComCadastroAsync();

        using HttpResponseMessage resposta = await EnviarAsync(id, """{ "chamadas": [{ "numeroDaChamada": 1, "pessoasDaReservaPorVaga": 2, "participacaoDaReserva": null }, { "numeroDaChamada": 2, "pessoasDaReservaPorVaga": 0, "participacaoDaReserva": "ASSUME_NA_MESMA_CHAMADA" }, { "numeroDaChamada": 3, "pessoasDaReservaPorVaga": 1, "participacaoDaReserva": "OUTRA" }] }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(static e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString())).Should().Equal(
                ("cadastroDeReserva[0].participacaoDaReserva", "uniplus.selecao.cadastro_de_reserva.participacao_ausente"),
                ("cadastroDeReserva[1].participacaoDaReserva", "uniplus.selecao.cadastro_de_reserva.participacao_com_quantidade_zero"),
                ("cadastroDeReserva[2].participacaoDaReserva", "uniplus.selecao.cadastro_de_reserva.participacao_desconhecida"));
        (await LerGravadoAsync(id)).Should().Equal(CadastroAnterior);
    }

    [Fact(DisplayName = "PUT sem o campo chamadas responde 400")]
    public async Task Put_SemChamadas_Retorna400()
    {
        Guid id = await SemearAsync();

        using HttpResponseMessage resposta = await EnviarAsync(id, "{}");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact(DisplayName = "PUT de processo inexistente responde 404")]
    public async Task Put_ProcessoInexistente_Retorna404()
    {
        using HttpResponseMessage resposta = await EnviarAsync(Guid.NewGuid(), """{ "chamadas": [] }""");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<Guid> SemearAsync()
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        OpcoesDoProcessoSeletivoPublicavel opcoes = new() { Nome = $"Cadastro de reserva {Guid.CreateVersion7()}" };
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, opcoes);
        return processo.Id;
    }

    private async Task<Guid> SemearComCadastroAsync()
    {
        Guid id = await SemearAsync();
        using HttpResponseMessage resposta = await EnviarAsync(id, CorpoDoCadastroAnterior);
        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await LerGravadoAsync(id)).Should().Equal(CadastroAnterior);
        return id;
    }

    private async Task<(int Chamada, int Pessoas, ParticipacaoDaReserva? Participacao)[]> LerGravadoAsync(Guid id)
    {
        await using AsyncServiceScope scope = fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        ProcessoSeletivo processo = await db.ProcessosSeletivos.AsNoTracking().SingleAsync(p => p.Id == id);
        return [.. processo.CadastroDeReserva.Chamadas.Select(static c => (c.Chamada.Valor, c.PessoasDaReservaPorVaga, c.Participacao))];
    }

    private async Task<HttpResponseMessage> EnviarAsync(Guid id, string corpo)
    {
        using HttpClient client = fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Put, new Uri($"/api/selecao/processos-seletivos/{id}/cadastro-reserva", UriKind.Relative))
        {
            Content = new StringContent(corpo, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        return await client.SendAsync(request);
    }
}
