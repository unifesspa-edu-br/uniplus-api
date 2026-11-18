namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.OfertasCurso.ApoioDosEndpointsDeOfertaCurso;

[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class OfertaCursoTotalEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;
    private readonly ApoioDosEndpointsDeOfertaCurso _apoio;

    public OfertaCursoTotalEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDosEndpointsDeOfertaCurso(fixture);
    }

    [Fact(DisplayName = "O total é o da consulta inteira, não o da página: 105 ofertas sob limit 100")]
    public async Task Listar_ComIncludeTotal_ContaAlemDaPagina()
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("total-count");
        await _apoio.SemearOfertasPorCursoAsync([dependencias.CursoId], dependencias.LocalOfertaId, dependencias.UnidadeId, quantidadePorCurso: 105);
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage response = await ListarAsync(client, $"cursoId={dependencias.CursoId}&include_total=true&limit=100");

        ConferirListagemDeOfertas(response);
        ConferirTotalMaiorQueAPagina(response);
    }

    [Fact(DisplayName = "Com busca, o total é o do filtro — nunca o da tabela inteira")]
    public async Task Listar_ComBuscaEIncludeTotal_ContaSomenteOFiltro()
    {
        string nomeCursoEngenhariaCivil = $"Engenharia Civil {Sufixo()}";
        (Guid cursoEngenhariaCivil, Guid localId) = await _apoio.SemearCursoELocalAsync(nomeCursoEngenhariaCivil);
        Guid cursoDireito = await _apoio.SemearCursoAsync($"Direito {Sufixo()}");
        Guid cursoSistemasInformacao = await _apoio.SemearCursoAsync($"Sistemas de Informação {Sufixo()}");
        Unidade unidade = await _apoio.SemearUnidadeAsync(NovoSlug("total-count-busca"), NovaSigla(), NovoCodigo());
        await _apoio.SemearOfertasPorCursoAsync([cursoEngenhariaCivil, cursoDireito, cursoSistemasInformacao], localId, unidade.Id, quantidadePorCurso: 10);
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage response = await ListarAsync(client, $"q={Uri.EscapeDataString(nomeCursoEngenhariaCivil)}&include_total=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("x-total-count").Single().Should().Be("10", "o total é contado sobre a mesma consulta que produziu a página, busca inclusive");
    }

    [Fact(DisplayName = "Consulta sem nenhum registro devolve X-Total-Count zero, não header ausente")]
    public async Task Listar_ComIncludeTotalSemRegistros_DevolveZero()
    {
        Guid cursoSemOfertas = await _apoio.SemearCursoAsync($"Curso sem oferta {Sufixo()}");
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage response = await ListarAsync(client, $"cursoId={cursoSemOfertas}&include_total=true");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ConferirZeroComoResposta(response);
    }

    [Fact(DisplayName = "O total acompanha criação e remoção, e a edição não o move")]
    public async Task Listar_TotalAcompanhaCriacaoEdicaoERemocao()
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("total-ciclo");
        await _apoio.SemearOfertasPorCursoAsync([dependencias.CursoId], dependencias.LocalOfertaId, dependencias.UnidadeId, quantidadePorCurso: 3);
        using HttpClient client = _fixture.Factory.CreateClient();
        await ConferirTotalAsync(client, dependencias.CursoId, "3");

        Guid criada = await CriarOfertaAsync(client, CorpoOferta(dependencias));
        await ConferirTotalAsync(client, dependencias.CursoId, "4", "a oferta recém-criada entra na contagem seguinte");
        await AtualizarParaRegimeIntegralAsync(client, criada);
        await ConferirTotalAsync(client, dependencias.CursoId, "4", "editar uma oferta não cria nem remove registro");
        await RemoverOfertaAsync(client, criada);
        // A remoção é lógica: a linha continua na tabela. O total só volta a 3 porque a contagem roda
        // sobre a consulta que já exclui as removidas — um COUNT sobre a tabela devolveria 4.
        await ConferirTotalAsync(client, dependencias.CursoId, "3", "a oferta removida sai da contagem");
    }

    [Theory(DisplayName = "Sem pedir o total, nenhum header de contagem é emitido")]
    [InlineData("")]
    [InlineData("&include_total=false")]
    public async Task Listar_SemPedirOTotal_NaoEmiteHeader(string parametroDoTotal)
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("sem-total");
        await _apoio.SemearOfertasPorCursoAsync([dependencias.CursoId], dependencias.LocalOfertaId, dependencias.UnidadeId, quantidadePorCurso: 1);
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpResponseMessage response = await ListarAsync(client, $"cursoId={dependencias.CursoId}{parametroDoTotal}");

        ConferirListagemDeOfertas(response);
        ConferirQueNaoHaHeaderDeTotal(response);
    }

    private static Task<HttpResponseMessage> ListarAsync(HttpClient client, string consulta) =>
        client.GetAsync(new Uri($"/api/configuracao/ofertas-curso?{consulta}", UriKind.Relative));

    private static void ConferirListagemDeOfertas(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.oferta-curso.v1+json");
    }

    // 100 é o teto de página (CursorPaginationOptions.LimitMax), e o volume semeado o
    // ultrapassa de propósito: os dois headers lado a lado é que provam a distinção —
    // com um total menor que o limite, a afirmação passaria mesmo se a contagem fosse
    // a da página. As 105 pertencem ao mesmo curso, que também é o CA-04 da #1511.
    private static void ConferirTotalMaiorQueAPagina(HttpResponseMessage response)
    {
        response.Headers.GetValues("x-total-count").Single().Should().Be("105");
        response.Headers.GetValues("x-page-size").Single().Should().Be("100");
    }

    // Zero é resposta, não ausência: o header omitido significaria "não perguntei",
    // e quem perguntou precisa distinguir a coleção vazia da contagem não pedida.
    private static void ConferirZeroComoResposta(HttpResponseMessage response) =>
        response.Headers.GetValues("x-total-count").Single().Should().Be("0");

    // O opt-in vale para a omissão tanto quanto para o false explícito (ADR-0026):
    // quem não pede não paga a contagem nem recebe o header.
    private static void ConferirQueNaoHaHeaderDeTotal(HttpResponseMessage response) =>
        response.Headers.Contains("x-total-count").Should().BeFalse();

    private static async Task ConferirTotalAsync(HttpClient client, Guid cursoId, string esperado, string porque = "") =>
        (await TotalDeOfertasDoCursoAsync(client, cursoId)).Should().Be(esperado, porque);

    private static async Task AtualizarParaRegimeIntegralAsync(HttpClient client, Guid id)
    {
        Dictionary<string, object?> atualizacao = CorpoDeAtualizacao(id, NovoCodigoDaOferta());
        atualizacao["regimeDeTurno"] = "INTEGRAL";
        atualizacao["turnos"] = new[] { "MATUTINO", "VESPERTINO" };

        using HttpResponseMessage atualizar = await EnviarPutAdmin(client, id, atualizacao);

        atualizar.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    private static async Task RemoverOfertaAsync(HttpClient client, Guid id)
    {
        using HttpResponseMessage remover = await EnviarDeleteAdmin(client, id);
        remover.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
