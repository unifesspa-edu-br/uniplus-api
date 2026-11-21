namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;

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

        using JsonDocument corpo = await LerProcessoAsync(client, processoId);
        JsonElement inscricao = FormularioDeInscricao(corpo);
        JsonElement coleta = inscricao.GetProperty("coleta");
        ConferirGrupoDaComposicaoFamiliar(coleta.GetProperty("grupos").EnumerateArray().Single(), campo);
        coleta.GetProperty("itens").EnumerateArray().Select(static f => f.GetProperty("fatoCodigo").GetString())
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
            .Should().Contain(["grupos[0].rotulo", "grupos[0].ocorrencias.minimo", "grupos[0].subitens", "grupos[0].subitens[0].apresentacao.rotulo"]);
    }

    [Fact(DisplayName = "O grupo sem as ocorrências é recusado com o código novo no mínimo e na inclusão do candidato, acumulado com as demais recusas")]
    public async Task DefinirItens_GrupoSemOcorrencias_RecusaMinimoEInclusaoJuntoDasDemais()
    {
        string campo = await SemearFatoDeMembroAsync();
        Guid processoId = await SemearProcessoAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        HttpResponseMessage recusado = await EnviarAsync(
            client, HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{processoId}/formularios/INSCRICAO/itens", CorpoComGrupoSemOcorrencias(campo), exigeSucesso: false);

        recusado.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        using JsonDocument problema = JsonDocument.Parse(await recusado.Content.ReadAsStringAsync());
        JsonElement[] erros = [.. problema.RootElement.GetProperty("errors").EnumerateArray()];
        erros.Select(static e => e.GetProperty("field").GetString()).Should().Contain(
            ["grupos[0].ocorrencias.minimo", "grupos[0].ocorrencias.incluiCandidato", "grupos[0].rotulo"]);
        erros.Where(static e => e.GetProperty("field").GetString()!.StartsWith("grupos[0].ocorrencias.", StringComparison.Ordinal))
            .Should().OnlyContain(static e => e.GetProperty("code").GetString() == "uniplus.grupo_formulario.ocorrencias_obrigatorias");
    }

    private static object CorpoComGrupoSemOcorrencias(string campo)
    {
        object[] subitens = [FormularioDeInscricaoHttp.Item(campo, 0, "Trabalha no campo", "BOOLEANO")];
        JsonObject grupo = JsonSerializer.SerializeToNode(FormularioDeInscricaoHttp.Grupo("COMPOSICAO_FAMILIAR", 0, "", "DADOS", subitens))!.AsObject();
        grupo.Remove("ocorrencias");
        return new { itens = Array.Empty<object>(), grupos = new[] { grupo } };
    }

    private static async Task<JsonDocument> LerProcessoAsync(HttpClient client, Guid processoId)
    {
        using HttpRequestMessage get = Requisicao(HttpMethod.Get, $"/api/selecao/processos-seletivos/{processoId}");
        get.Headers.Accept.ParseAdd("application/vnd.uniplus.processo-seletivo.v1+json");
        HttpResponseMessage lido = await client.SendAsync(get);
        lido.StatusCode.Should().Be(HttpStatusCode.OK, await lido.Content.ReadAsStringAsync());
        return JsonDocument.Parse(await lido.Content.ReadAsStringAsync());
    }

    private static JsonElement FormularioDeInscricao(JsonDocument processo) =>
        processo.RootElement.GetProperty("formularios").EnumerateArray()
            .Single(static f => f.GetProperty("finalidade").GetString() == "INSCRICAO");

    private static void ConferirGrupoDaComposicaoFamiliar(JsonElement grupo, string campo)
    {
        grupo.GetProperty("codigo").GetString().Should().Be("COMPOSICAO_FAMILIAR");
        JsonElement ocorrencias = grupo.GetProperty("ocorrencias");
        ocorrencias.GetProperty("maximo").ValueKind.Should().Be(JsonValueKind.Null);
        ocorrencias.GetProperty("incluiCandidato").GetBoolean().Should().BeTrue();
        grupo.GetProperty("subitens").EnumerateArray().Select(static s => s.GetProperty("fatoCodigo").GetString())
            .Should().Equal(CandidatoComoMembro.FatoParentesco, campo);
    }

    /// <summary>O PUT dos itens com a composição familiar sem máximo que inclui o candidato.</summary>
    private static Task<HttpResponseMessage> DefinirGrupoAsync(
        HttpClient client, Guid processoId, string campo, bool comParentesco,
        string rotulo = "Composição familiar", int minimo = 1, string rotuloDoCampo = "Trabalha no campo")
    {
        object[] subitens =
        [
            .. comParentesco
                ? new object[] { FormularioDeInscricaoHttp.Item(CandidatoComoMembro.FatoParentesco, 0, "Parentesco", "SELECAO_UNICA") }
                : [],
            FormularioDeInscricaoHttp.Item(campo, 1, rotuloDoCampo, "BOOLEANO"),
        ];
        return EnviarAsync(client, HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{processoId}/formularios/INSCRICAO/itens", new
        {
            itens = Array.Empty<object>(),
            grupos = new[] { FormularioDeInscricaoHttp.Grupo("COMPOSICAO_FAMILIAR", 0, rotulo, "DADOS", subitens, new OcorrenciasDoGrupoInput(minimo, true)) },
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
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await SemearProcessoPublicavelAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        await DefinirOpcoesDeCategoriaDeRendaAsync(client, processo.Id, membro);
        await DefinirGrupoDeUmCampoAsync(
            client, processo.Id, FormularioDeInscricaoHttp.Item(membro, 0, "Categoria de renda", "SELECAO_UNICA"), obrigatoriedadeDoGrupo);
        await ExigirDocumentoAsync(client, processo, tipoDocumentoId, (agregado, "EM", "[\"RURAL\"]"));

        HttpResponseMessage publicado = await PublicarAsync(client, processo.Id, documento.Id);
        publicado.StatusCode.Should().Be(esperado, await publicado.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "Exigência repetida por membro do grupo, com gatilho pelo campo do grupo, é aceita e publicada")]
    public async Task Publicar_ExigenciaRepetidaPorMembroComGatilhoDoCampo_Publica()
    {
        string sufixo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        string sobGuarda = $"SOB_GUARDA_{sufixo}";
        FatoCandidato campoDeMembro = FatoBooleanoDeMembro(sobGuarda, "Sob guarda", "RESULTADO_FINAL");
        Guid tipoDocumentoId = await SemearNoCatalogoAsync(campoDeMembro, $"CERT_GUARDA_{sufixo}", "Certidão de guarda");
        (ProcessoSeletivo processo, DocumentoAPublicar documento) = await SemearProcessoPublicavelAsync();
        using HttpClient client = _fixture.Factory.CreateClient();

        await DefinirGrupoDeUmCampoAsync(client, processo.Id, FormularioDeInscricaoHttp.Item(sobGuarda, 0, "Sob guarda", "BOOLEANO"), "SEMPRE");
        await ExigirDocumentoAsync(client, processo, tipoDocumentoId, (sobGuarda, "IGUAL", "true"), repetePorEntidade: "COMPOSICAO_FAMILIAR");

        HttpResponseMessage publicado = await PublicarAsync(client, processo.Id, documento.Id);
        publicado.StatusCode.Should().Be(HttpStatusCode.NoContent, await publicado.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> DefinirOpcoesDeCategoriaDeRendaAsync(HttpClient client, Guid processoId, string fato) =>
        EnviarAsync(client, HttpMethod.Put, $"/api/selecao/processos-seletivos/{processoId}/fatos/{fato}/opcoes", new
        {
            opcoes = new[] { new { codigo = "RURAL", rotulo = "Trabalhador rural" }, new { codigo = "URBANA", rotulo = "Trabalhador urbano" } },
        });

    /// <summary>O PUT dos itens com a composição familiar de um só campo, de uma a dez ocorrências.</summary>
    private static Task<HttpResponseMessage> DefinirGrupoDeUmCampoAsync(HttpClient client, Guid processoId, object campo, string obrigatoriedade) =>
        EnviarAsync(client, HttpMethod.Put, $"/api/selecao/admin/processos-seletivos/{processoId}/formularios/INSCRICAO/itens", new
        {
            itens = Array.Empty<object>(),
            grupos = new[]
            {
                FormularioDeInscricaoHttp.Grupo(
                    "COMPOSICAO_FAMILIAR", 0, "Composição familiar", "DADOS", [campo], new OcorrenciasDoGrupoInput(1, false, 10), obrigatoriedade),
            },
        });

    /// <summary>
    /// A exigência da folha na fase do processo, para quem cumpre a <paramref name="condicao"/> — fato, operador e valor —,
    /// repetida por membro quando o grupo é dado em <paramref name="repetePorEntidade"/>.
    /// </summary>
    private static Task<HttpResponseMessage> ExigirDocumentoAsync(
        HttpClient client, ProcessoSeletivo processo, Guid tipoDocumentoId, (string Fato, string Operador, string Valor) condicao, string? repetePorEntidade = null) =>
        EnviarAsync(
            client, HttpMethod.Put, $"/api/selecao/processos-seletivos/{processo.Id}/documentos-exigidos",
            new[] { new { tipo = "FOLHA", repetePorEntidade, documento = DocumentoDaFolha(processo, tipoDocumentoId, condicao) } });

    private static object DocumentoDaFolha(ProcessoSeletivo processo, Guid tipoDocumentoId, (string Fato, string Operador, string Valor) condicao) => new
    {
        tipoDocumento = new { id = tipoDocumentoId },
        momento = new { faseId = processo.CronogramaFases.Single().Id, finalidade = "INSCRICAO" },
        aplicabilidade = new
        {
            tipo = "CONDICIONAL",
            condicoes = new[] { new { clausula = 0, fato = condicao.Fato, operador = condicao.Operador, valor = condicao.Valor } },
        },
        indeferimento = new
        {
            obrigatorio = true,
            basesLegais = new[] { new { referencia = "Res. Unifesspa 532/2021", abrangencia = "INTERNA_NORMA", status = "RESOLVIDO" } },
        },
        requisitos = new { formatosPermitidos = "QUALQUER" },
    };

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
                dataDoAto = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            },
        });

    /// <summary>Grava os fatos e um tipo de documento no catálogo da Configuração; devolve o tipo de documento.</summary>
    private async Task<Guid> SemearNoCatalogoAsync(FatoCandidato fato, string codigoDoDocumento, string nomeDoDocumento, params FatoCandidato[] outros)
    {
        TipoDocumento tipoDocumento = TipoDocumento.Criar(
            codigoDoDocumento, nomeDoDocumento, descricao: null, categoria: "IDENTIFICACAO",
            formatosAceitos: null, tamanhoMaximoMb: null, tipoEquivalente: null, classificacaoProtecao: ClassificacaoDeProtecao.Pessoal, finalidadeTratamento: "Comprovação documental do candidato.", hipoteseLegal: HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        db.AddRange([fato, .. outros]);
        db.Add(tipoDocumento);
        await db.SaveChangesAsync();
        await db.AprovarCatalogoAsync();
        return tipoDocumento.Id;
    }

    private async Task<Guid> SemearCatalogoDoAgregadoAsync(string membro, string agregado, string sufixo)
    {
        FatoCandidato fatoDeMembro = CriacaoDoFato.CriarDoAdministrador(
            membro, "Categoria de renda", "Descrição do fato.", DominioFato.Categorico, CardinalidadeFato.Escalar, FonteValoresFato.Processo, formato: null,
            "RESULTADO_FINAL", EscopoFato.MembroGrupo, ClassificacaoDeProtecao.Pessoal, "Composição familiar",
            HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
        FatoCandidato fatoAgregado = CriacaoDoFato.CriarAgregadoDoAdministrador(
            agregado, "Categorias de renda da família", "Descrição do fato.", membro, new CatalogoDeFatos([fatoDeMembro], []),
            "RESULTADO_FINAL", ClassificacaoDeProtecao.Pessoal, "Composição familiar", HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;
        return await SemearNoCatalogoAsync(fatoDeMembro, $"DECL_RURAL_{sufixo}", "Declaração de trabalhador rural", fatoAgregado);
    }

    private async Task<(ProcessoSeletivo Processo, DocumentoAPublicar Documento)> SemearProcessoPublicavelAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        await TiposDeAtoSeeder.SemearAsync(_fixture.Factory.Services);
        return await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"Agregado do grupo {Guid.CreateVersion7()}" });
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

    private static FatoCandidato FatoBooleanoDeMembro(string codigo, string nome, string pontoResolucao) =>
        CriacaoDoFato.CriarDoAdministrador(
            codigo, nome, "Descrição do fato.", DominioFato.Booleano, CardinalidadeFato.Escalar, fonteValores: null, formato: null,
            pontoResolucao, EscopoFato.MembroGrupo, ClassificacaoDeProtecao.Pessoal, "Composição familiar",
            HipoteseLegalTratamento.CumprimentoObrigacaoLegal).Value!;

    private async Task<string> SemearFatoDeMembroAsync()
    {
        string codigo = $"MEMBRO_{Guid.NewGuid():N}"[..30].ToUpperInvariant();
        FatoCandidato fato = FatoBooleanoDeMembro(codigo, "Trabalha no campo", "INSCRICAO");
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        db.Add(fato);
        await db.SaveChangesAsync();
        await db.AprovarCatalogoAsync();
        return codigo;
    }

    private async Task<Guid> SemearProcessoAsync()
    {
        await using AsyncServiceScope escopo = _fixture.Factory.Services.CreateAsyncScope();
        UniPlusDbContext db = escopo.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        (ProcessoSeletivo processo, _) = await ProcessoSeletivoPublicavelSeeder.SemearAsync(db, new OpcoesDoProcessoSeletivoPublicavel { Nome = $"Grupo repetível {Guid.CreateVersion7()}" });
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
