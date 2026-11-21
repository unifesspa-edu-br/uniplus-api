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
public sealed class OfertaCursoRegimesEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;
    private readonly ApoioDosEndpointsDeOfertaCurso _apoio;

    public OfertaCursoRegimesEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new ApoioDosEndpointsDeOfertaCurso(fixture);
    }

    [Theory(DisplayName = "POST com regime de turno e turnos incoerentes retorna 422 pelo wire code correspondente")]
    [InlineData(null, new[] { "MATUTINO" }, "uniplus.configuracao.oferta_curso.regime_de_turno_obrigatorio")]
    [InlineData("PARCIAL", new[] { "MATUTINO" }, "uniplus.configuracao.oferta_curso.regime_de_turno_invalido")]
    [InlineData("REGULAR", new string[0], "uniplus.configuracao.oferta_curso.turnos_obrigatorios")]
    [InlineData("REGULAR", new[] { "INTEGRAL" }, "uniplus.configuracao.oferta_curso.turno_invalido")]
    [InlineData("REGULAR", new[] { "DIURNO" }, "uniplus.configuracao.oferta_curso.turno_invalido")]
    [InlineData("INTEGRAL", new[] { "MATUTINO", "MATUTINO" }, "uniplus.configuracao.oferta_curso.turno_repetido")]
    [InlineData("REGULAR", new[] { "MATUTINO", "NOTURNO" }, "uniplus.configuracao.oferta_curso.cardinalidade_turnos_incompativel_com_regime")]
    [InlineData("INTEGRAL", new[] { "NOTURNO" }, "uniplus.configuracao.oferta_curso.cardinalidade_turnos_incompativel_com_regime")]
    public async Task Criar_RegimeOuTurnosInvalidos_Retorna422ComWireCode(
        string? regimeDeTurno, string[] turnos, string wireCodeEsperado)
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("regime");
        Dictionary<string, object?> corpo = CorpoOferta(dependencias);
        corpo["regimeDeTurno"] = regimeDeTurno;
        corpo["turnos"] = turnos;

        await _apoio.ConferirRecusaNaCriacaoAsync(
            corpo, wireCodeEsperado, "cada violação de regime ou turno tem wire code próprio no catálogo público");
    }

    [Theory(DisplayName = "POST com regime de funcionamento ausente, nulo ou fora do domínio retorna 422 pelo wire code correspondente")]
    [InlineData(null, "uniplus.configuracao.oferta_curso.regime_de_funcionamento_obrigatorio")]
    [InlineData("", "uniplus.configuracao.oferta_curso.regime_de_funcionamento_obrigatorio")]
    [InlineData("   ", "uniplus.configuracao.oferta_curso.regime_de_funcionamento_obrigatorio")]
    [InlineData("SEMI_INTENSIVO", "uniplus.configuracao.oferta_curso.regime_de_funcionamento_invalido")]
    [InlineData("Intensivo", "uniplus.configuracao.oferta_curso.regime_de_funcionamento_invalido")]
    [InlineData("INTEGRAL", "uniplus.configuracao.oferta_curso.regime_de_funcionamento_invalido")]
    public async Task Criar_RegimeDeFuncionamentoInvalido_Retorna422ComWireCode(
        string? regimeDeFuncionamento, string wireCodeEsperado)
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("func");
        Dictionary<string, object?> corpo = CorpoOferta(dependencias);
        corpo["regimeDeFuncionamento"] = regimeDeFuncionamento;

        await _apoio.ConferirRecusaNaCriacaoAsync(
            corpo, wireCodeEsperado, "ausência e token fora do domínio têm wire codes distintos no catálogo público");
    }

    [Fact(DisplayName = "POST omitindo o campo regimeDeFuncionamento retorna 422 de obrigatório — o campo ausente chega à validação de domínio")]
    public async Task Criar_SemCampoRegimeDeFuncionamento_Retorna422()
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("omisso");
        Dictionary<string, object?> corpo = CorpoOferta(dependencias);
        corpo.Remove("regimeDeFuncionamento");
        corpo["regimeDeTurno"] = "INTEGRAL";
        corpo["turnos"] = new[] { "MATUTINO", "VESPERTINO" };

        await _apoio.ConferirRecusaNaCriacaoAsync(
            corpo,
            "uniplus.configuracao.oferta_curso.regime_de_funcionamento_obrigatorio",
            "dois turnos sob INTEGRAL não presumem oferta intensiva");
    }

    [Fact(DisplayName = "POST de oferta intensiva com regime de turno regular retorna 422 de incompatibilidade e não persiste")]
    public async Task Criar_IntensivoComRegular_Retorna422ENaoPersiste()
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("intensivo");
        Dictionary<string, object?> corpo = CorpoOferta(dependencias);
        corpo["regimeDeFuncionamento"] = "INTENSIVO";

        await _apoio.ConferirRecusaNaCriacaoAsync(
            corpo, "uniplus.configuracao.oferta_curso.regime_de_funcionamento_incompativel_com_regime_de_turno");

        IReadOnlyList<OfertaCurso> persistidas = await _apoio.OfertasGravadasAsync(dependencias.CursoId, dependencias.LocalOfertaId);
        persistidas.Should().BeEmpty("a recusa de domínio precede qualquer escrita");
    }

    [Fact(DisplayName = "POST de oferta intensiva integral é criada e a leitura devolve as duas dimensões declaradas")]
    public async Task Criar_IntensivoIntegral_CriaEDevolveAsDuasDimensoes()
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("intint");
        Dictionary<string, object?> corpo = CorpoIntensivoIntegral(dependencias);

        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarOfertaAsync(client, corpo);

        using JsonDocument doc = await LerOfertaAsync(client, id);
        JsonElement root = doc.RootElement;
        root.GetProperty("regimeDeFuncionamento").GetProperty("codigo").GetString().Should().Be("INTENSIVO");
        root.GetProperty("regimeDeTurno").GetProperty("codigo").GetString().Should().Be("INTEGRAL");
        root.GetProperty("turnos").EnumerateArray().Select(t => t.GetProperty("codigo").GetString())
            .Should().Equal(["MATUTINO", "VESPERTINO"]);
    }

    [Fact(DisplayName = "POST em alternância pedagógica é criada, o PUT troca o regime e a leitura devolve o valor declarado")]
    public async Task CicloAlternanciaPedagogica_CriaAtualizaELe()
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("alternancia");
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarOfertaAsync(client, CorpoEmAlternancia(dependencias));
        await ConferirRegimesDaOfertaAsync(client, id, "ALTERNANCIA_PEDAGOGICA", "REGULAR");

        Guid idParaAtualizacao = await CriarOfertaAsync(client, CorpoOferta(dependencias));
        using HttpResponseMessage atualizar = await EnviarPutAdmin(client, idParaAtualizacao, AtualizacaoEmAlternanciaNoVespertino(idParaAtualizacao));

        atualizar.StatusCode.Should().Be(HttpStatusCode.NoContent);
        await ConferirRegimesDaOfertaAsync(client, idParaAtualizacao, "ALTERNANCIA_PEDAGOGICA", "REGULAR");
    }

    [Fact(DisplayName = "PUT que torna a oferta intensiva sem trocar o regime de turno retorna 422 e não converte os valores")]
    public async Task Atualizar_ParaIntensivoMantendoRegular_Retorna422SemConverter()
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("putint");
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid id = await CriarOfertaAsync(client, CorpoOferta(dependencias));
        Dictionary<string, object?> atualizacao = CorpoDeAtualizacao(id, NovoCodigoDaOferta());
        atualizacao["regimeDeFuncionamento"] = "INTENSIVO";

        using HttpResponseMessage atualizar = await EnviarPutAdmin(client, id, atualizacao);

        await ConferirRecusa422Async(
            atualizar, "uniplus.configuracao.oferta_curso.regime_de_funcionamento_incompativel_com_regime_de_turno");
        await ConferirRegimesDaOfertaAsync(
            client, id, "EXTENSIVO", "REGULAR", "a recusa preserva o valor declarado, sem conversão silenciosa");
    }

    [Theory(DisplayName = "POST sem turno é recusado em qualquer formato pedagógico — a distância inclusive")]
    [InlineData("EAD")]
    [InlineData("SEMIPRESENCIAL")]
    [InlineData("PRESENCIAL")]
    public async Task Criar_SemTurnoEmQualquerFormato_Retorna422(string formatoPedagogico)
    {
        DependenciasDaOferta dependencias = await _apoio.SemearDependenciasAsync("formato");
        Dictionary<string, object?> corpo = CorpoOferta(dependencias);
        corpo["formatoPedagogico"] = formatoPedagogico;
        corpo["turnos"] = Array.Empty<string>();

        await _apoio.ConferirRecusaNaCriacaoAsync(
            corpo, "uniplus.configuracao.oferta_curso.turnos_obrigatorios", "nenhum formato pedagógico abre exceção ao turno");
    }

    private static Dictionary<string, object?> CorpoIntensivoIntegral(DependenciasDaOferta dependencias)
    {
        Dictionary<string, object?> corpo = CorpoOferta(dependencias);
        corpo["regimeDeFuncionamento"] = "INTENSIVO";
        corpo["regimeDeTurno"] = "INTEGRAL";
        corpo["turnos"] = new[] { "MATUTINO", "VESPERTINO" };
        return corpo;
    }

    private static Dictionary<string, object?> CorpoEmAlternancia(DependenciasDaOferta dependencias)
    {
        Dictionary<string, object?> corpo = CorpoOferta(dependencias);
        corpo["regimeDeFuncionamento"] = "ALTERNANCIA_PEDAGOGICA";
        return corpo;
    }

    private static Dictionary<string, object?> AtualizacaoEmAlternanciaNoVespertino(Guid id)
    {
        Dictionary<string, object?> atualizacao = CorpoDeAtualizacao(id, NovoCodigoDaOferta());
        atualizacao["regimeDeFuncionamento"] = "ALTERNANCIA_PEDAGOGICA";
        atualizacao["turnos"] = new[] { "VESPERTINO" };
        return atualizacao;
    }

    private static async Task ConferirRegimesDaOfertaAsync(
        HttpClient client, Guid id, string regimeDeFuncionamento, string regimeDeTurno, string porque = "")
    {
        using JsonDocument doc = await LerOfertaAsync(client, id);
        doc.RootElement.GetProperty("regimeDeFuncionamento").GetProperty("codigo").GetString().Should().Be(regimeDeFuncionamento, porque);
        doc.RootElement.GetProperty("regimeDeTurno").GetProperty("codigo").GetString().Should().Be(regimeDeTurno);
    }
}
