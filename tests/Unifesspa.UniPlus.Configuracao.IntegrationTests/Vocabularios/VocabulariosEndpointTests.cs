namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Vocabularios;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Regras.Enums;

/// <summary>
/// Contrato HTTP dos vocabulários fechados de Configuração — <c>TipoBanca</c> e
/// <c>FaseCanonica</c> (UNI-REQ-0139), e <c>TipoInstrumentoNormativo</c>, usado pela Base
/// Legal de Bônus Regional. O que se
/// prova aqui é o que o fitness test não alcança: que as rotas existem, respondem sem
/// autenticação, negociam a vendor MIME e não têm par de escrita.
/// </summary>
[Collection(ConfiguracaoEndpointCollection.Name)]
public sealed class VocabulariosEndpointTests
{
    private readonly ConfiguracaoEndpointFixture _fixture;

    public VocabulariosEndpointTests(ConfiguracaoEndpointFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "GET /vocabularios/grupos-area-enem devolve os quatro grupos com código e rótulo, na ordem, sem exigir autenticação")]
    public async Task ListarGruposAreaEnem_DevolveOsQuatroGrupos()
    {
        using HttpClient client = _fixture.Factory.CreateDefaultClient();

        GrupoAreaEnemDto[] grupos = await ApoioDosVocabularios.LerAsync<GrupoAreaEnemDto>(
            client, "grupos-area-enem", "codigo-grupo-area-enem");

        // Afirmado por extenso: é o contrato publicado que está sob teste, não a lista do domínio.
        grupos.Should().Equal(
            new GrupoAreaEnemDto("TECNOLOGICA", "Tecnológica"),
            new GrupoAreaEnemDto("HUMANISTICA_I", "Humanística I"),
            new GrupoAreaEnemDto("HUMANISTICA_II", "Humanística II"),
            new GrupoAreaEnemDto("SAUDE_E_BIOLOGICAS", "Saúde e Biológicas"));
    }

    [Fact(DisplayName = "GET /vocabularios/tipos-banca devolve os dez códigos com rótulo, na ordem publicada, sem exigir autenticação")]
    public async Task ListarCodigosTipoBanca_DevolveVocabularioCompleto()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        CodigoComRotulo<TipoDeBanca>[] codigos = await ApoioDosVocabularios.LerAsync<CodigoComRotulo<TipoDeBanca>>(
            client, "tipos-banca", "codigo-tipo-banca");

        codigos.Select(c => c.Codigo).Should().Equal(CodigosDeTipoBancaNaOrdemPublicada);
        codigos.Should().AllSatisfy(c => c.Rotulo.Should().NotBeNullOrWhiteSpace());
        codigos.Select(static c => (c.Codigo, c.Rotulo)).Should().Contain(
            ("BANCA_ANALISE_RECURSOS_SEGUNDA_INSTANCIA", "Banca de análise de recursos de 2ª instância"));
    }

    [Fact(DisplayName = "GET /vocabularios/papeis-de-banca devolve os cinco papéis com código e rótulo, na ordem publicada, sem exigir autenticação")]
    public async Task ListarPapeisDeBanca_DevolveOsCincoPapeis()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        CodigoComRotulo<PapelDaBanca>[] papeis =
            await ApoioDosVocabularios.LerAsync<CodigoComRotulo<PapelDaBanca>>(client, "papeis-de-banca", "codigo-papel-de-banca");

        // Afirmado por extenso: é o contrato publicado que está sob teste, não a lista do domínio.
        papeis.Select(static p => (p.Codigo, p.Rotulo)).Should().Equal(
            ("AVALIA_O_MERITO", "Avalia o mérito"),
            ("JULGA_A_PRIMEIRA_INSTANCIA", "Julga o recurso em 1ª instância"),
            ("IMPORTA_AS_NOTAS_DO_INEP", "Importa as notas do INEP"),
            ("IMPORTA_CANDIDATOS", "Importa candidatos"),
            ("JULGA_A_SEGUNDA_INSTANCIA", "Julga o recurso em 2ª instância"));
    }

    [Fact(DisplayName = "GET /vocabularios/tipos-de-decisao devolve os oito tipos de decisão com código e rótulo, na ordem publicada, sem exigir autenticação")]
    public async Task ListarTiposDeDecisao_DevolveOsOitoTipos()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        CodigoComRotulo<TipoMotivoDeDecisao>[] tipos =
            await ApoioDosVocabularios.LerAsync<CodigoComRotulo<TipoMotivoDeDecisao>>(client, "tipos-de-decisao", "codigo-tipo-de-decisao");

        tipos.Select(static t => (t.Codigo, t.Rotulo)).Should().Equal(
            ("ISENCAO_DA_TAXA", "Isenção da taxa"),
            ("HOMOLOGACAO_DAS_INSCRICOES", "Homologação das inscrições"),
            ("ANALISE_DOCUMENTAL", "Análise documental"),
            ("HETEROIDENTIFICACAO", "Heteroidentificação"),
            ("AVALIACAO_BIOPSICOSSOCIAL", "Avaliação biopsicossocial"),
            ("CLASSIFICACAO", "Classificação"),
            ("GABARITO_DA_PROVA", "Gabarito da prova"),
            ("NOTA_DA_ETAPA", "Nota da etapa"));
    }

    // Afirmado por extenso, e não derivado do catálogo: é o contrato publicado que está
    // sob teste — derivá-lo da mesma fonte que o produz faria o teste concordar com
    // qualquer mudança, inclusive uma remoção acidental de código.
    private static readonly string[] CodigosDeTipoBancaNaOrdemPublicada =
    [
        "BANCA_ANALISE_DOCUMENTAL",
        "BANCA_ENTREVISTA",
        "BANCA_CORRECAO_REDACOES",
        "BANCA_ANALISE_RECURSOS",
        "BANCA_HETEROIDENTIFICACAO",
        "BANCA_BIOPSICOSSOCIAL",
        "BANCA_HOMOLOGACAO_INSCRICOES",
        "BANCA_IMPORTACAO_NOTAS_INEP",
        "BANCA_IMPORTACAO_CANDIDATOS",
        "BANCA_ANALISE_RECURSOS_SEGUNDA_INSTANCIA",
    ];

    private static readonly string[] CodigosDeFaseNaOrdemPublicada =
    [
        "INSCRICAO",
        "SOLICITACAO_ISENCAO",
        "HOMOLOGACAO_INSCRICOES",
        "ENSALAMENTO",
        "AVALIACAO",
        "CLASSIFICACAO",
        "RESULTADO_FINAL",
        "HABILITACAO",
        "HETEROIDENTIFICACAO",
        "AVALIACAO_BIOPSICOSSOCIAL",
        "MATRICULA",
        "HOMOLOGACAO_RESULTADO_FINAL",
        "LISTA_ESPERA",
        "CHAMADA",
    ];

    [Fact(DisplayName = "GET /vocabularios/fases-canonicas devolve os quatorze códigos com nome, na ordem publicada, sem exigir autenticação")]
    public async Task ListarCodigosFaseCanonica_DevolveVocabularioCompleto()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        FaseCanonicaVocabularioDto[] codigos = await ApoioDosVocabularios.LerAsync<FaseCanonicaVocabularioDto>(
            client, "fases-canonicas", "codigo-fase-canonica");

        codigos.Select(c => c.Codigo).Should().Equal(CodigosDeFaseNaOrdemPublicada);
        codigos.Should().AllSatisfy(c => c.Nome.Should().NotBeNullOrWhiteSpace());
    }

    // Afirmado por extenso, pelo mesmo motivo da lista de tipos de banca.
    private static readonly string[] CodigosDeInstrumentoNormativoNaOrdemPublicada =
    [
        "LEI",
        "DECRETO",
        "PORTARIA",
        "RESOLUCAO",
        "INSTRUCAO_NORMATIVA",
        "PARECER",
        "EDITAL",
    ];

    [Fact(DisplayName = "GET /vocabularios/tipos-instrumento-normativo devolve os tipos com código, nome e descrição, na ordem publicada, sem exigir autenticação")]
    public async Task ListarCodigosTipoInstrumentoNormativo_DevolveVocabularioCompleto()
    {
        using HttpClient client = _fixture.Factory.CreateClient();

        TipoInstrumentoNormativoVocabularioDto[] codigos =
            await ApoioDosVocabularios.LerAsync<TipoInstrumentoNormativoVocabularioDto>(
                client, "tipos-instrumento-normativo", "codigo-tipo-instrumento-normativo");

        codigos.Select(c => c.Codigo).Should().Equal(CodigosDeInstrumentoNormativoNaOrdemPublicada);
        codigos.Should().AllSatisfy(c =>
        {
            c.Nome.Should().NotBeNullOrWhiteSpace();
            c.Descricao.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Theory(DisplayName = "Vocabulário governado por código não tem rota de escrita — evolução é mudança versionada da API, não cadastro")]
    [InlineData("/api/configuracao/vocabularios/tipos-banca")]
    [InlineData("/api/configuracao/vocabularios/papeis-de-banca")]
    [InlineData("/api/configuracao/vocabularios/tipos-de-decisao")]
    [InlineData("/api/configuracao/vocabularios/fases-canonicas")]
    [InlineData("/api/configuracao/vocabularios/tipos-instrumento-normativo")]
    [InlineData("/api/configuracao/vocabularios/grupos-area-enem")]
    public async Task Vocabularios_NaoTemRotaDeEscrita(string rota)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        Uri uri = new(rota, UriKind.Relative);

        HttpResponseMessage post = await client.PostAsJsonAsync(uri, new { codigo = "X" });
        HttpResponseMessage put = await client.PutAsJsonAsync(uri, new { codigo = "X" });
        HttpResponseMessage delete = await client.DeleteAsync(uri);

        // 405 é a resposta de quem tem a rota e não o verbo. Um 401/403 aqui significaria
        // que a rota de escrita existe e só está protegida — e uma proteção se afrouxa.
        post.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        put.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        delete.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
    }

    [Theory(DisplayName = "Versão inexistente da vendor MIME recusa com 406 em vez de servir a v1 em silêncio")]
    [InlineData("/api/configuracao/vocabularios/tipos-banca", "codigo-tipo-banca")]
    [InlineData("/api/configuracao/vocabularios/papeis-de-banca", "codigo-papel-de-banca")]
    [InlineData("/api/configuracao/vocabularios/tipos-de-decisao", "codigo-tipo-de-decisao")]
    [InlineData("/api/configuracao/vocabularios/fases-canonicas", "codigo-fase-canonica")]
    [InlineData("/api/configuracao/vocabularios/tipos-instrumento-normativo", "codigo-tipo-instrumento-normativo")]
    [InlineData("/api/configuracao/vocabularios/grupos-area-enem", "codigo-grupo-area-enem")]
    public async Task Vocabularios_VersaoDesconhecidaDaVendorMime_Recusa(string rota, string resource)
    {
        using HttpClient client = _fixture.Factory.CreateClient();
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(rota, UriKind.Relative));
        request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse($"application/vnd.uniplus.{resource}.v9+json"));

        HttpResponseMessage response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotAcceptable);
    }
}
