namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// O grupo repetível pelo HTTP, contra o Postgres (UNI-REQ-0146): o PUT dos itens grava o grupo com
/// os campos de membro, e o GET do processo o devolve no formulário.
/// </summary>
[Collection(CascadingCollection.Name)]
[Trait("Category", "OutboxCapability")]
[Trait("Category", "OutboxCascading")]
public sealed class GrupoRepetivelEndpointTests
{
    private readonly CascadingFixture _fixture;

    public GrupoRepetivelEndpointTests(CascadingFixture fixture) => _fixture = fixture;

    [Fact(DisplayName = "O PUT dos itens grava o grupo sem máximo que inclui o candidato, e o GET do processo o devolve")]
    public async Task DefinirItens_ComGrupo_GravaEDevolveNoFormulario()
    {
        string campo = await SemearFatoDeMembroAsync();
        Guid processoId = await SemearProcessoAsync();

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage gravado = await DefinirGrupoAsync(client, processoId, campo, comParentesco: true);
        gravado.StatusCode.Should().Be(HttpStatusCode.NoContent, await gravado.Content.ReadAsStringAsync());

        using HttpRequestMessage get = Requisicao(HttpMethod.Get, $"/api/selecao/processos-seletivos/{processoId}");
        get.Headers.Accept.ParseAdd("application/vnd.uniplus.processo-seletivo.v1+json");
        HttpResponseMessage lido = await client.SendAsync(get);
        lido.StatusCode.Should().Be(HttpStatusCode.OK, await lido.Content.ReadAsStringAsync());

        using JsonDocument corpo = JsonDocument.Parse(await lido.Content.ReadAsStringAsync());
        JsonElement inscricao = corpo.RootElement.GetProperty("formularios").EnumerateArray()
            .Single(static f => f.GetProperty("finalidade").GetString() == "INSCRICAO");
        JsonElement grupo = inscricao.GetProperty("grupos").EnumerateArray().Single();
        grupo.GetProperty("codigo").GetString().Should().Be("COMPOSICAO_FAMILIAR");
        grupo.GetProperty("maximo").ValueKind.Should().Be(JsonValueKind.Null);
        grupo.GetProperty("incluiCandidato").GetBoolean().Should().BeTrue();
        grupo.GetProperty("subitens").EnumerateArray().Select(static s => s.GetProperty("fatoCodigo").GetString())
            .Should().Equal(CandidatoComoMembro.FatoParentesco, campo);
        inscricao.GetProperty("fatosColetados").EnumerateArray().Select(static f => f.GetProperty("fatoCodigo").GetString())
            .Should().NotContain(campo, "o campo do grupo não é item do formulário");
    }

    [Fact(DisplayName = "O grupo que inclui o candidato sem o campo de parentesco é recusado no mesmo lote das demais recusas do grupo")]
    public async Task DefinirItens_GrupoQueIncluiOCandidatoSemParentesco_RecusaJuntoDasDemais()
    {
        string campo = await SemearFatoDeMembroAsync();
        Guid processoId = await SemearProcessoAsync();

        using HttpClient client = _fixture.Factory.CreateClient();
        HttpResponseMessage recusado = await DefinirGrupoAsync(client, processoId, campo, comParentesco: false, rotulo: "", minimo: 0, rotuloDoCampo: "");

        recusado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await recusado.Content.ReadAsStringAsync());
        problema.RootElement.GetProperty("errors").EnumerateArray().Select(static e => e.GetProperty("field").GetString())
            .Should().Contain(["grupos[0].rotulo", "grupos[0].minimo", "grupos[0].subitens", "grupos[0].subitens[0].rotulo"]);
    }

    /// <summary>O PUT dos itens com a composição familiar sem máximo que inclui o candidato.</summary>
    private static Task<HttpResponseMessage> DefinirGrupoAsync(
        HttpClient client, Guid processoId, string campo, bool comParentesco,
        string rotulo = "Composição familiar", int minimo = 1, string rotuloDoCampo = "Trabalha no campo")
    {
        object[] subitens =
        [
            .. comParentesco
                ? new object[] { new { fatoCodigo = CandidatoComoMembro.FatoParentesco, ordem = 0, rotulo = "Parentesco", tipoRenderizacao = "SELECAO_UNICA", obrigatoriedade = "SEMPRE" } }
                : [],
            new { fatoCodigo = campo, ordem = 1, rotulo = rotuloDoCampo, tipoRenderizacao = "BOOLEANO", obrigatoriedade = "SEMPRE" },
        ];
        return EnviarAsync(client, HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{processoId}/formularios/INSCRICAO/itens", new
        {
            itens = Array.Empty<object>(),
            grupos = new[]
            {
                new
                {
                    codigo = "COMPOSICAO_FAMILIAR", ordem = 0, rotulo, etapaCodigo = "DADOS",
                    minimo, maximo = (int?)null, incluiCandidato = true, obrigatoriedade = "NUNCA", subitens,
                },
            },
        }, exigeSucesso: false);
    }

    [Theory(DisplayName = "Documento exigido pelo agregado do grupo, com as opções do fato de membro, publica só com o grupo obrigatório")]
    [InlineData("SEMPRE", HttpStatusCode.NoContent)]
    [InlineData("NUNCA", HttpStatusCode.UnprocessableEntity)]
    public async Task Publicar_ExigenciaPeloAgregadoDoGrupo_PublicaSoComGrupoObrigatorio(string obrigatoriedadeDoGrupo, HttpStatusCode esperado)
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        string membro = $"CATEGORIA_RENDA_{sufixo}";
        string agregado = $"CATEGORIAS_FAMILIA_{sufixo}";
        Guid tipoDocumentoId = await SemearCatalogoDoAgregadoAsync(membro, agregado, sufixo);
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        (ProcessoSeletivo processo, DocumentoEdital documento) = await SemearProcessoPublicavelAsync();
        Guid faseId = processo.CronogramaFases.Single().Id;
        using HttpClient client = _fixture.Factory.CreateClient();

        await EnviarAsync(client, HttpMethod.Put, $"/api/selecao/processos-seletivos/{processo.Id}/fatos/{membro}/opcoes", new
        {
            opcoes = new[] { new { codigo = "RURAL", rotulo = "Trabalhador rural" }, new { codigo = "URBANA", rotulo = "Trabalhador urbano" } },
        });
        await EnviarAsync(client, HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{processo.Id}/formularios/INSCRICAO/itens", new
        {
            itens = Array.Empty<object>(),
            grupos = new[]
            {
                new
                {
                    codigo = "COMPOSICAO_FAMILIAR", ordem = 0, rotulo = "Composição familiar", etapaCodigo = "DADOS",
                    minimo = 1, maximo = 10, obrigatoriedade = obrigatoriedadeDoGrupo,
                    subitens = new[] { new { fatoCodigo = membro, ordem = 0, rotulo = "Categoria de renda", tipoRenderizacao = "SELECAO_UNICA", obrigatoriedade = "SEMPRE" } },
                },
            },
        });
        await EnviarAsync(client, HttpMethod.Put, $"/api/selecao/processos-seletivos/{processo.Id}/documentos-exigidos", new[]
        {
            new
            {
                tipo = "FOLHA",
                documento = new
                {
                    exigidoNaFaseId = faseId,
                    tipoDocumentoId,
                    aplicabilidade = "CONDICIONAL",
                    obrigatorio = true,
                    condicoes = new[] { new { clausula = 0, fato = agregado, operador = "EM", valor = "[\"RURAL\"]" } },
                    basesLegais = new[] { new { referencia = "Res. Unifesspa 532/2021", abrangencia = "INTERNA_NORMA", status = "RESOLVIDO" } },
                    formatosPermitidos = "QUALQUER",
                },
            },
        });

        HttpResponseMessage publicado = await PublicarAsync(client, processo.Id, documento.Id);
        publicado.StatusCode.Should().Be(esperado, await publicado.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "Exigência repetida por membro do grupo, com gatilho pelo campo do grupo, é aceita e publicada")]
    public async Task Publicar_ExigenciaRepetidaPorMembroComGatilhoDoCampo_Publica()
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        string sobGuarda = $"SOB_GUARDA_{sufixo}";
        FatoCandidato campoDeMembro = FatoCandidato.CriarDoAdministrador(
            sobGuarda, "Sob guarda", null, DominioFato.Booleano, CardinalidadeFato.Escalar, fonteValores: null, formato: null,
            "RESULTADO_FINAL", EscopoFato.MembroGrupo, ClassificacaoProtecaoDado.Pessoal, "Composição familiar",
            HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
        Guid tipoDocumentoId = await SemearNoCatalogoAsync(campoDeMembro, $"CERT_GUARDA_{sufixo}", "Certidão de guarda");
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        (ProcessoSeletivo processo, DocumentoEdital documento) = await SemearProcessoPublicavelAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        await EnviarAsync(client, HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{processo.Id}/formularios/INSCRICAO/itens", new
        {
            itens = Array.Empty<object>(),
            grupos = new[]
            {
                new
                {
                    codigo = "COMPOSICAO_FAMILIAR", ordem = 0, rotulo = "Composição familiar", etapaCodigo = "DADOS",
                    minimo = 1, maximo = 10, obrigatoriedade = "SEMPRE",
                    subitens = new[] { new { fatoCodigo = sobGuarda, ordem = 0, rotulo = "Sob guarda", tipoRenderizacao = "BOOLEANO", obrigatoriedade = "SEMPRE" } },
                },
            },
        });
        await EnviarAsync(client, HttpMethod.Put, $"/api/selecao/processos-seletivos/{processo.Id}/documentos-exigidos", new[]
        {
            new
            {
                tipo = "FOLHA",
                repetePorEntidade = "COMPOSICAO_FAMILIAR",
                documento = new
                {
                    exigidoNaFaseId = processo.CronogramaFases.Single().Id,
                    tipoDocumentoId,
                    aplicabilidade = "CONDICIONAL",
                    obrigatorio = true,
                    condicoes = new[] { new { clausula = 0, fato = sobGuarda, operador = "IGUAL", valor = "true" } },
                    basesLegais = new[] { new { referencia = "Res. Unifesspa 532/2021", abrangencia = "INTERNA_NORMA", status = "RESOLVIDO" } },
                    formatosPermitidos = "QUALQUER",
                },
            },
        });

        HttpResponseMessage publicado = await PublicarAsync(client, processo.Id, documento.Id);
        publicado.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicado.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> PublicarAsync(HttpClient client, Guid processoId, Guid documentoEditalId) =>
        EnviarAsync(client, HttpMethod.Post, $"/api/selecao/processos-seletivos/{processoId}/publicacao", exigeSucesso: false, corpo: new
        {
            documentoEditalId,
            ato = new
            {
                orgao = "CEPS",
                serie = "EDITAL",
                ano = 2026,
                assinante = "Diretor do CEPS",
                tipoAtoCodigo = "EDITAL_ABERTURA",
                dataPublicacao = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            },
        });

    /// <summary>Grava os fatos e um tipo de documento no catálogo da Configuração; devolve o tipo de documento.</summary>
    private async Task<Guid> SemearNoCatalogoAsync(FatoCandidato fato, string codigoDoDocumento, string nomeDoDocumento, params FatoCandidato[] outros)
    {
        TipoDocumento tipoDocumento = TipoDocumento.Criar(
            codigoDoDocumento, nomeDoDocumento, descricao: null, categoria: "IDENTIFICACAO",
            formatosAceitos: null, tamanhoMaximoMb: null, tipoEquivalente: null).Value!;
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext db = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        db.AddRange([fato, .. outros]);
        db.Add(tipoDocumento);
        await db.SaveChangesAsync();
        return tipoDocumento.Id;
    }

    private async Task<Guid> SemearCatalogoDoAgregadoAsync(string membro, string agregado, string sufixo)
    {
        FatoCandidato fatoDeMembro = FatoCandidato.CriarDoAdministrador(
            membro, "Categoria de renda", null, DominioFato.Categorico, CardinalidadeFato.Escalar, FonteValoresFato.Processo, formato: null,
            "RESULTADO_FINAL", EscopoFato.MembroGrupo, ClassificacaoProtecaoDado.Pessoal, "Composição familiar",
            HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
        FatoCandidato fatoAgregado = FatoCandidato.CriarAgregadoDoAdministrador(
            agregado, "Categorias de renda da família", null, membro, new CatalogoDeFatos([fatoDeMembro], []),
            "RESULTADO_FINAL", ClassificacaoProtecaoDado.Pessoal, "Composição familiar", HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
        return await SemearNoCatalogoAsync(fatoDeMembro, $"DECL_RURAL_{sufixo}", "Declaração de trabalhador rural", fatoAgregado);
    }

    private async Task<(ProcessoSeletivo Processo, DocumentoEdital Documento)> SemearProcessoPublicavelAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        return await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, $"Agregado do grupo {Guid.CreateVersion7()}");
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient client, HttpMethod metodo, string rota, object corpo, bool exigeSucesso = true)
    {
        using HttpRequestMessage request = Requisicao(metodo, rota);
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.CreateVersion7().ToString("N"));
        request.Content = JsonContent.Create(corpo);
        HttpResponseMessage resposta = await client.SendAsync(request);
        if (exigeSucesso)
        {
            resposta.IsSuccessStatusCode.Should().BeTrue($"{metodo} {rota}: {await resposta.Content.ReadAsStringAsync()}");
        }

        return resposta;
    }

    private async Task<string> SemearFatoDeMembroAsync()
    {
        string codigo = $"MEMBRO_{Guid.NewGuid():N}"[..30].ToUpperInvariant();
        FatoCandidato fato = FatoCandidato.CriarDoAdministrador(
            codigo, "Trabalha no campo", null, DominioFato.Booleano, CardinalidadeFato.Escalar, fonteValores: null, formato: null,
            "INSCRICAO", EscopoFato.MembroGrupo, ClassificacaoProtecaoDado.Pessoal, "Composição familiar",
            HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        ConfiguracaoDbContext db = escopo.ServiceProvider.GetRequiredService<ConfiguracaoDbContext>();
        db.Add(fato);
        await db.SaveChangesAsync();
        return codigo;
    }

    private async Task<Guid> SemearProcessoAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        SelecaoDbContext db = escopo.ServiceProvider.GetRequiredService<SelecaoDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, $"Grupo repetível {Guid.CreateVersion7()}");
        return processo.Id;
    }

    private static HttpRequestMessage Requisicao(HttpMethod metodo, string rota)
    {
        HttpRequestMessage request = new(metodo, new Uri(rota, UriKind.Relative));
        request.Headers.Authorization = new AuthenticationHeaderValue(TestAuthHandler.AuthorizationScheme, TestAuthHandler.TokenValue);
        request.Headers.TryAddWithoutValidation(TestAuthHandler.RolesHeader, "plataforma-admin");
        return request;
    }
}
