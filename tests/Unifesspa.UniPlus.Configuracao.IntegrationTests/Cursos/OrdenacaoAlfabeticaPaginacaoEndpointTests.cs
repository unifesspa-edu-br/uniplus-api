namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Cursos;

using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Pagination;

using static Unifesspa.UniPlus.Configuracao.IntegrationTests.Cursos.ApoioDaOrdenacaoAlfabetica;

[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class OrdenacaoAlfabeticaPaginacaoEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;
    private readonly ApoioDaOrdenacaoAlfabetica _apoio;

    public OrdenacaoAlfabeticaPaginacaoEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDaOrdenacaoAlfabetica(fixture);
    }

    [Fact(DisplayName = "A travessia por rel=\"next\" mantém a ordem global e não repete nem omite registro")]
    public async Task Cursos_TravessiaCompleta_MantemOrdemSemRepetirNemOmitir()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        // Sete cursos com limit=2: mais registros do que cabe numa página, e a
        // última página fica incompleta.
        string[] sufixos = ["G", "C", "A", "F", "B", "E", "D"];
        foreach (string sufixo in sufixos)
        {
            await CriarCursoAsync(client, $"{bloco} {sufixo}");
        }

        List<string> percorridos = [];
        List<string> paginas = [];
        string? url = $"{Cursos}?limit=2";

        while (url is not null)
        {
            HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute));
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);

            IReadOnlyList<string> nomes = await LerNomesAsync(resposta);
            percorridos.AddRange(nomes.Where(n => n.StartsWith(bloco, StringComparison.Ordinal)));
            paginas.Add(LinkRel(resposta, "self") ?? url);
            url = LinkRel(resposta, "next");
        }

        string[] esperados = [.. sufixos.Order(StringComparer.Ordinal).Select(s => $"{bloco} {s}")];
        percorridos.Should().Equal(esperados);
        percorridos.Should().OnlyHaveUniqueItems();
        paginas.Count.Should().BeGreaterThan(1, "sete registros não cabem numa página de dois");
    }

    [Fact(DisplayName = "rel=\"prev\" volta exatamente à página anterior, na mesma ordem")]
    public async Task Cursos_Prev_VoltaAPaginaAnterior()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        foreach (string sufixo in new[] { "A", "B", "C", "D" })
        {
            await CriarCursoAsync(client, $"{bloco} {sufixo}");
        }

        // Navega até achar a página que abre o bloco, depois avança uma e volta.
        HttpResponseMessage primeira = await SeguirAtePaginaComAsync(client, bloco, $"{Cursos}?limit=2");
        IReadOnlyList<string> nomesPrimeira = await LerNomesAsync(primeira);

        string proxima = LinkRel(primeira, "next").Should().NotBeNull().And.Subject.ToString()!;
        HttpResponseMessage segunda = await client.GetAsync(new Uri(proxima, UriKind.RelativeOrAbsolute));

        string anterior = LinkRel(segunda, "prev").Should().NotBeNull().And.Subject.ToString()!;
        HttpResponseMessage volta = await client.GetAsync(new Uri(anterior, UriKind.RelativeOrAbsolute));

        (await LerNomesAsync(volta)).Should().Equal(nomesPrimeira);
    }

    [Fact(DisplayName = "Ofertas de curso saem na ordem alfabética do curso que ofertam")]
    public async Task Ofertas_SaemNaOrdemAlfabeticaDoCurso()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        // Ofertas criadas fora da ordem alfabética dos cursos que ofertam: se a
        // listagem devolvesse a ordem de gravação, a asserção falharia.
        Guid ofertaZoologia = await _apoio.SemearOfertaAsync($"{bloco} Zoologia");
        Guid ofertaMedicina = await _apoio.SemearOfertaAsync($"{bloco} Medicina");
        Guid ofertaAgronomia = await _apoio.SemearOfertaAsync($"{bloco} Agronomia");

        IReadOnlyList<JsonElement> itens = await ListarTudoAsync(client, OfertasCurso);
        Guid[] doBloco =
        [
            .. itens
                .Select(i => i.GetProperty("id").GetGuid())
                .Where(id => id == ofertaZoologia || id == ofertaMedicina || id == ofertaAgronomia),
        ];

        doBloco.Should().Equal(ofertaAgronomia, ofertaMedicina, ofertaZoologia);
    }

    [Fact(DisplayName = "O filtro por curso continua valendo ao seguir rel=\"next\"")]
    public async Task Ofertas_FiltroPorCurso_SobreviveAPaginacao()
    {
        string bloco = Bloco();
        using HttpClient client = _fixture.Factory.CreateClient();

        Guid curso = await _apoio.SemearCursoAsync($"{bloco} Filtrado");
        Guid outro = await _apoio.SemearCursoAsync($"{bloco} Ignorado");
        Guid local = await _apoio.SemearLocalOfertaAsync();

        List<Guid> doCurso =
        [
            await _apoio.SemearOfertaDeAsync(curso, local),
            await _apoio.SemearOfertaDeAsync(curso, local),
            await _apoio.SemearOfertaDeAsync(curso, local),
        ];
        await _apoio.SemearOfertaDeAsync(outro, local);

        List<Guid> percorridos = [];
        string? url = $"{OfertasCurso}?cursoId={curso}&limit=2";
        while (url is not null)
        {
            HttpResponseMessage resposta = await client.GetAsync(new Uri(url, UriKind.RelativeOrAbsolute));
            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            percorridos.AddRange((await LerItensAsync(resposta)).Select(i => i.GetProperty("id").GetGuid()));
            url = LinkRel(resposta, "next");
        }

        percorridos.Should().BeEquivalentTo(doCurso, "o recorte por curso vale em todas as páginas");
        percorridos.Should().OnlyHaveUniqueItems();
    }

    [Fact(DisplayName = "Cursor cuja chave de ordenação não corresponde à ordenação da rota devolve 400")]
    public async Task Cursor_ComChaveDeOrdenacaoIncompativel_Retorna400()
    {
        string cursor = await _apoio.CursorDeOutraOrdenacaoAsync();

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"{Cursos}?cursor={Uri.EscapeDataString(cursor)}", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await resposta.Content.ReadAsStringAsync()).Should().Contain("uniplus.paginacao.cursor_invalido");
    }

    /// <summary>
    /// A recusa do cursor que falha no BINDING continua com o envelope da paginação, e não com
    /// o genérico das cargas que não desserializam.
    /// </summary>
    /// <remarks>
    /// <para>
    /// As duas saem pelo mesmo gancho do MVC, e ele é uma cadeia: cada registro captura o
    /// anterior e o chama quando a falha não é dele. O tradutor genérico é o TERMINAL — tem de
    /// ficar na ponta, e fica porque é instalado no primeiro registro do pipeline de erro.
    /// Instalado depois, ele passaria a ser o primeiro a responder e engoliria esta recusa, que
    /// sabe dizer muito mais sobre a causa.
    /// </para>
    /// <para>
    /// A outra recusa de cursor já coberta aqui não serve como guarda: ela nasce no handler, e
    /// não no binding, então nem chega ao gancho. Esta força o binder a reprovar, mandando um
    /// cursor que não decodifica.
    /// </para>
    /// </remarks>
    [Fact(DisplayName = "Cursor que não decodifica é recusado pelo envelope da paginação, não pelo genérico de binding")]
    public async Task Listar_CursorMalformado_MantemOEnvelopeDaPaginacao()
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage resposta = await client.GetAsync(
            new Uri($"{Cursos}?cursor=isto-nao-e-um-cursor", UriKind.Relative));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        string corpo = await resposta.Content.ReadAsStringAsync();
        corpo.Should().Contain("uniplus.cursor.invalido",
            "a precedência inverteu: o tradutor genérico de binding respondeu no lugar do da paginação");
    }
}
