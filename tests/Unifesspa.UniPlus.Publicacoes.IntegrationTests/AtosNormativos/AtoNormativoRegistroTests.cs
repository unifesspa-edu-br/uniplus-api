namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.AtosNormativos;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.IntegrationTests.Infrastructure;

using static AtoNormativoApoio;

/// <summary>
/// Registro de <c>AtoNormativo</c> por HTTP: resolução do tipo vigente, cópia por
/// valor, número opcional e validação do corpo.
/// </summary>
[Collection(PublicacoesEndpointCollection.Name)]
public sealed class AtoNormativoRegistroTests
{
    private static readonly (string?, string?)[] ErrosEsperadosDoValidator =
    [
        ("orgao", "uniplus.validacao.obrigatorio"),
        ("serie", "uniplus.validacao.tamanho"),
        ("ano", "uniplus.validacao.faixa"),
        ("documentoHash", "uniplus.validacao.formato"),
        ("versaoDoObjetoHash", "uniplus.validacao.regra"),
        ("vinculos[0].entidadeTipo", "uniplus.validacao.formato"),
        ("vinculos[0].entidadeId", "uniplus.validacao.obrigatorio"),
    ];

    private readonly PublicacoesEndpointFixture _fixture;
    private readonly AtoNormativoApoio _apoio;

    public AtoNormativoRegistroTests(PublicacoesEndpointFixture fixture)
    {
        _fixture = fixture;
        _apoio = new AtoNormativoApoio(fixture);
    }

    [Fact(DisplayName = "POST registra, devolve 201 com Location e copia congela/efeito do catálogo")]
    public async Task Registrar_Retorna201_CopiaConsequencia()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync(congela: true, efeito: true);
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await PostAtoAsync(client, PayloadAto(tipo, SerieUnica(), "13"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location!.AbsolutePath.Should().StartWith($"{BaseAtos}/");
        Guid atoId = await LerAtoIdDoRegistroAsync(response);
        JsonElement detalhe = await LerAtoAsync(client, atoId);
        detalhe.GetProperty("congelaConfiguracao").GetBoolean().Should().BeTrue();
        detalhe.GetProperty("efeitoIrreversivel").GetBoolean().Should().BeTrue();
        detalhe.GetProperty("_links").GetProperty("self").GetString().Should().Be($"{BaseAtos}/{atoId}");
    }

    [Fact(DisplayName = "POST copia o nome do tipo vigente, e renomear o tipo não reescreve o ato")]
    public async Task Registrar_CopiaNomeDoTipo_RenomearNaoReescreve()
    {
        (string tipo, Guid tipoId) = await _apoio.CadastrarTipoVigenteAsync(nome: "Edital de abertura");
        using HttpClient client = _fixture.Factory.CreateClient();
        Guid atoId = await PostAtoIdAsync(client, PayloadAto(tipo, SerieUnica(), "12"));
        (await LerAtoAsync(client, atoId)).GetProperty("tipoNome").GetString().Should().Be("Edital de abertura");

        (await RenomearTipoAsync(client, tipoId, tipo)).Should().Be(HttpStatusCode.NoContent);

        (await LerAtoAsync(client, atoId)).GetProperty("tipoNome").GetString()
            .Should().Be("Edital de abertura", "o nome foi copiado por valor no registro, e o cadastro editável não reescreve o passado");
    }

    [Fact(DisplayName = "Ato registrado sem o nome do tipo devolve o nome ausente e o código presente")]
    public async Task Obter_AtoSemNomeDoTipo_NomeAusenteCodigoPresente()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync();
        Guid atoId = await GravarAtoSemNomeDoTipoAsync(tipo);
        using HttpClient client = _fixture.Factory.CreateClient();

        JsonElement ato = await LerAtoAsync(client, atoId);

        (ato.TryGetProperty("tipoNome", out JsonElement nome) ? nome.ValueKind : JsonValueKind.Undefined)
            .Should().BeOneOf(JsonValueKind.Null, JsonValueKind.Undefined);
        ato.GetProperty("tipoCodigo").GetString().Should().Be(tipo);
    }

    [Fact(DisplayName = "POST de ato sem número é aceito (número é opcional)")]
    public async Task Registrar_SemNumero_Retorna201()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await PostAtoAsync(client, PayloadAto(tipo, SerieUnica(), numero: null));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact(DisplayName = "POST de tipo sem versão vigente na data retorna 422 nomeado")]
    public async Task Registrar_TipoSemVigencia_Retorna422()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await PostAtoAsync(
            client, PayloadAto("TIPO_INEXISTENTE_" + Sufixo(), SerieUnica(), "1"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await response.Content.ReadAsStringAsync())
            .Should().Contain("uniplus.publicacoes.ato_normativo.tipo_sem_versao_vigente");
    }

    [Fact(DisplayName = "POST com par de versão incompleto retorna 422")]
    public async Task Registrar_VersaoDoObjetoIncompleta_Retorna422()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await PostAtoAsync(
            client, PayloadComVersaoDoObjeto(tipo, Guid.CreateVersion7(), versaoHash: null));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact(DisplayName = "POST com id de versão zerado (Guid.Empty) retorna 422, não 500")]
    public async Task Registrar_VersaoDoObjetoIdVazio_Retorna422()
    {
        string tipo = await _apoio.CriarTipoVigenteAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await PostAtoAsync(
            client, PayloadComVersaoDoObjeto(tipo, Guid.Empty, HashValido));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    /// <summary>
    /// O 422 do validator FluentValidation publica cada violação na taxonomia
    /// <c>uniplus.validacao.*</c> e com o campo no casing do payload — e não o nome da classe
    /// interna do FluentValidation (<c>NotEmptyValidator</c>, <c>MaximumLengthValidator</c>…)
    /// nem o nome C# da propriedade, que vazavam para o contrato consumido pelo frontend.
    /// O payload dispara uma violação de cada categoria, inclusive dentro da coleção de
    /// vínculos e em regras com <c>OverridePropertyName</c>.
    /// </summary>
    [Fact(DisplayName = "POST reprovado no validator publica errors[] na taxonomia e com field em camelCase")]
    public async Task Registrar_ReprovadoNoValidator_ErrorsNaTaxonomiaComFieldEmCamelCase()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage response = await PostAtoAsync(client, PayloadReprovadoNoValidator());

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("code").GetString().Should().Be("uniplus.validacao");
        (string?, string?)[] erros = ExtrairErros(problema);
        erros.Should().AllSatisfy(e => e.Item2.Should().MatchRegex(@"^uniplus\.validacao\.[a-z_]+$"));
        erros.Should().BeEquivalentTo(ErrosEsperadosDoValidator);
    }

    private static object PayloadReprovadoNoValidator() =>
        new
        {
            orgao = "",
            serie = new string('A', 101),
            ano = 0,
            numero = "1",
            tipoCodigo = "QUALQUER",
            dataDoAto = Publicacao,
            documentoHash = "nao-e-hash",
            assinante = "Jairo Belchior",
            versaoDoObjetoId = Guid.CreateVersion7(),
            versaoDoObjetoHash = (string?)null,
            vinculos = new[] { new { entidadeTipo = "minusculo", entidadeId = Guid.Empty } },
        };

    private static (string?, string?)[] ExtrairErros(JsonDocument problema) =>
        [.. problema.RootElement.GetProperty("errors").EnumerateArray()
            .Select(e => (e.GetProperty("field").GetString(), e.GetProperty("code").GetString()))];

    private static async Task<Guid> LerAtoIdDoRegistroAsync(HttpResponseMessage response)
    {
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        doc.RootElement.GetProperty("publicadoEm").GetDateTimeOffset().Should().NotBe(default);
        return doc.RootElement.GetProperty("atoId").GetGuid();
    }

    private static async Task<HttpStatusCode> RenomearTipoAsync(HttpClient client, Guid tipoId, string codigo)
    {
        using HttpRequestMessage put = new(HttpMethod.Put, new Uri($"{AdminTipos}/{tipoId}", UriKind.Relative));
        Autenticar(put);
        put.Content = JsonContent.Create(PayloadTipoRenomeado(tipoId, codigo));

        return (await client.SendAsync(put)).StatusCode;
    }

    private static object PayloadTipoRenomeado(Guid tipoId, string codigo) =>
        new
        {
            id = tipoId,
            codigo,
            nome = "Edital de abertura renomeado",
            congelaConfiguracao = false,
            unicoPorObjeto = false,
            efeitoIrreversivel = false,
            publicavelComoAvulso = false,
            exigeAtoCorrigido = false,
            vigenciaInicio = "2020-01-01",
            vigenciaFim = (string?)null,
        };

    private async Task<Guid> GravarAtoSemNomeDoTipoAsync(string tipoCodigo)
    {
        AtoNormativo semNome = AtoNormativo.Registrar(
            Guid.CreateVersion7(), "CEPS", SerieUnica(), 2026, "1", tipoCodigo,
            congelaConfiguracao: false, efeitoIrreversivel: false, unicoPorObjeto: false,
            dataDoAto: new DateOnly(2026, 3, 13),
            documentoHash: HashValido,
            assinante: "Jairo Belchior",
            publicadoEm: DateTimeOffset.UtcNow,
            versaoDoObjeto: null);
        await using AsyncServiceScope scope = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext ctx = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await ctx.Set<AtoNormativo>().AddAsync(semNome);
        await ctx.SaveChangesAsync();
        return semNome.Id;
    }
}
