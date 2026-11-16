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
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync(NovoSlug("total-count"), NovaSigla(), NovoCodigo());
        await _apoio.SemearOfertasAsync(cursoId, localId, unidade.Id, quantidade: 105);

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(
            new Uri(
                $"/api/configuracao/ofertas-curso?cursoId={cursoId}&include_total=true&limit=100",
                UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.oferta-curso.v1+json");

        // 100 é o teto de página (CursorPaginationOptions.LimitMax), e o volume semeado o
        // ultrapassa de propósito: os dois headers lado a lado é que provam a distinção —
        // com um total menor que o limite, a afirmação passaria mesmo se a contagem fosse
        // a da página. As 105 pertencem ao mesmo curso, que também é o CA-04 da #1511.
        response.Headers.GetValues("x-total-count").Single().Should().Be("105");
        response.Headers.GetValues("x-page-size").Single().Should().Be("100");
    }

    [Fact(DisplayName = "Com busca, o total é o do filtro — nunca o da tabela inteira")]
    public async Task Listar_ComBuscaEIncludeTotal_ContaSomenteOFiltro()
    {
        string nomeCursoEngenhariaCivil = $"Engenharia Civil {Sufixo()}";
        (Guid cursoEngenhariaCivil, Guid localId) =
            await _apoio.SemearCursoELocalParaBuscaAsync(nomeCursoEngenhariaCivil);
        Guid cursoDireito = await _apoio.SemearCursoAsync($"Direito {Sufixo()}");
        Guid cursoSistemasInformacao = await _apoio.SemearCursoAsync($"Sistemas de Informação {Sufixo()}");
        Unidade unidade = await _apoio.SemearUnidadeAsync(
            NovoSlug("total-count-busca"), NovaSigla(), NovoCodigo());

        foreach (Guid cursoId in new[] { cursoEngenhariaCivil, cursoDireito, cursoSistemasInformacao })
        {
            await _apoio.SemearOfertasAsync(cursoId, localId, unidade.Id, quantidade: 10);
        }

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(
            new Uri(
                $"/api/configuracao/ofertas-curso?q={Uri.EscapeDataString(nomeCursoEngenhariaCivil)}&include_total=true",
                UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("x-total-count").Single().Should().Be("10",
            "o total é contado sobre a mesma consulta que produziu a página, busca inclusive");
    }

    [Fact(DisplayName = "Consulta sem nenhum registro devolve X-Total-Count zero, não header ausente")]
    public async Task Listar_ComIncludeTotalSemRegistros_DevolveZero()
    {
        Guid cursoSemOfertas = await _apoio.SemearCursoAsync($"Curso sem oferta {Sufixo()}");

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(
            new Uri(
                $"/api/configuracao/ofertas-curso?cursoId={cursoSemOfertas}&include_total=true",
                UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Zero é resposta, não ausência: o header omitido significaria "não perguntei",
        // e quem perguntou precisa distinguir a coleção vazia da contagem não pedida.
        response.Headers.GetValues("x-total-count").Single().Should().Be("0");
    }

    [Fact(DisplayName = "O total acompanha criação e remoção, e a edição não o move")]
    public async Task Listar_TotalAcompanhaCriacaoEdicaoERemocao()
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync(NovoSlug("total-ciclo"), NovaSigla(), NovoCodigo());
        await _apoio.SemearOfertasAsync(cursoId, localId, unidade.Id, quantidade: 3);

        using HttpClient client = _fixture.Factory.CreateClient();
        (await TotalDeOfertasDoCursoAsync(client, cursoId)).Should().Be("3");

        HttpResponseMessage criar = await EnviarPostAdmin(
            client, CorpoOferta(cursoId, localId, unidade.Id));
        criar.StatusCode.Should().Be(HttpStatusCode.Created);
        Guid criada = await criar.Content.ReadFromJsonAsync<Guid>();
        (await TotalDeOfertasDoCursoAsync(client, cursoId)).Should().Be("4",
            "a oferta recém-criada entra na contagem seguinte");

        HttpResponseMessage atualizar = await EnviarPutAdmin(client, criada, new
        {
            id = criada,
            codigo = NovoCodigoDaOferta(),
            programaDeOferta = "REGULAR",
            regimeDeFuncionamento = "EXTENSIVO",
            regimeDeTurno = "INTEGRAL",
            turnos = new[] { "MATUTINO", "VESPERTINO" },
        });
        atualizar.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await TotalDeOfertasDoCursoAsync(client, cursoId)).Should().Be("4",
            "editar uma oferta não cria nem remove registro");

        HttpResponseMessage remover = await EnviarDeleteAdmin(client, criada);
        remover.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // A remoção é lógica: a linha continua na tabela. O total só volta a 3 porque a
        // contagem roda sobre a consulta que já carrega o filtro global de exclusão —
        // um COUNT sobre a tabela devolveria 4 e o contador da tela passaria a mentir.
        (await TotalDeOfertasDoCursoAsync(client, cursoId)).Should().Be("3",
            "a oferta removida sai da contagem");
    }

    [Theory(DisplayName = "Sem pedir o total, nenhum header de contagem é emitido")]
    [InlineData("")]
    [InlineData("&include_total=false")]
    public async Task Listar_SemPedirOTotal_NaoEmiteHeader(string parametroDoTotal)
    {
        (Guid cursoId, Guid localId) = await _apoio.SemearCursoELocalAsync();
        Unidade unidade = await _apoio.SemearUnidadeAsync(NovoSlug("sem-total"), NovaSigla(), NovoCodigo());
        await _apoio.SemearOfertasAsync(cursoId, localId, unidade.Id, quantidade: 1);

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage response = await client.GetAsync(
            new Uri(
                $"/api/configuracao/ofertas-curso?cursoId={cursoId}{parametroDoTotal}",
                UriKind.Relative));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType
            .Should().Be("application/vnd.uniplus.oferta-curso.v1+json");

        // O opt-in vale para a omissão tanto quanto para o false explícito (ADR-0026):
        // quem não pede não paga a contagem nem recebe o header.
        response.Headers.Contains("x-total-count").Should().BeFalse();
    }
}
