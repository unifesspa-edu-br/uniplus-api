namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Xunit;

/// <summary>
/// <b>Fronteira de tipo do contrato público do certame.</b>
/// </summary>
/// <remarks>
/// <para>
/// A configuração congelada cresce a cada incremento do domínio. Sem esta verificação, o bloco
/// seguinte nasce e alguém, projetando por conveniência, o publica sem que ninguém tenha decidido
/// que ele é público — é o risco de exposição excessiva no nível da propriedade: servir o objeto
/// inteiro e entregar campo interno que nunca se pretendeu expor.
/// </para>
/// <para>
/// Com ela, acrescentar bloco ao envelope <b>quebra a construção</b> até que a pessoa o classifique
/// como público, interno, ou público por outro contrato. A decisão de exposição deixa de depender
/// de revisão humana atenta e vira obrigação verificável.
/// </para>
/// <para>
/// É a contrapartida da projeção por tipos declarados, que já impede o vazamento por omissão. Esta
/// suíte cobre o caso em que alguém contorna a forma.
/// </para>
/// </remarks>
public sealed class FronteiraDeBlocosDoCertameTests
{
    [Fact(DisplayName = "Todo bloco da configuração congelada está classificado como público ou interno")]
    public void Classificacao_QuandoEnvelopeDeReferencia_NaoDeveTerBlocoSemClassificar()
    {
        IReadOnlyCollection<string> naoClassificados = NaoClassificados(ChavesDoEnvelopeDeReferencia());

        naoClassificados.Should().BeEmpty(
            "todo bloco da configuração congelada precisa ser classificado antes de existir — os não classificados são: {0}",
            string.Join(", ", naoClassificados));
    }

    [Fact(DisplayName = "O envelope com os blocos condicionais presentes também está inteiramente classificado")]
    public void Classificacao_QuandoEnvelopeTemBlocosCondicionais_NaoDeveTerBlocoSemClassificar()
    {
        // Cascata e bônus regional alternam presença; sem a variante em que existem, escapam.
        SnapshotCanonico canonico = EnvelopeCanonicoGoldenTests.CanonicalizarReferenciaComCascata();
        IReadOnlyCollection<string> naoClassificados = NaoClassificados(ChavesDe(canonico));

        naoClassificados.Should().BeEmpty(
            "os blocos condicionais também precisam de classificação — os não classificados são: {0}",
            string.Join(", ", naoClassificados));
    }

    [Fact(DisplayName = "Um bloco fictício não classificado faz a verificação falhar, nomeando o bloco")]
    public void Classificacao_QuandoEnvelopeGanhaBlocoNaoClassificado_DeveQuebrarNomeandoOBloco()
    {
        // O teste do próprio teste.
        List<string> comBlocoNovo = [.. ChavesDoEnvelopeDeReferencia(), "dimensaoAindaNaoDecidida"];

        IReadOnlyCollection<string> naoClassificados = NaoClassificados(comBlocoNovo);

        naoClassificados.Should().ContainSingle().Which.Should().Be("dimensaoAindaNaoDecidida");
    }

    [Fact(DisplayName = "Nenhum bloco é classificado em duas categorias ao mesmo tempo")]
    public void Classificacao_QuandoCategoriasSaoComparadas_NaoDevemSeSobrepor()
    {
        // Bloco em duas categorias passaria na verificação com a exposição indefinida.
        ClassificacaoDosBlocosDoCertame.Publicados
            .Intersect(ClassificacaoDosBlocosDoCertame.Internos, StringComparer.Ordinal)
            .Should().BeEmpty();

        ClassificacaoDosBlocosDoCertame.Publicados
            .Intersect(ClassificacaoDosBlocosDoCertame.PublicosPorOutroContrato, StringComparer.Ordinal)
            .Should().BeEmpty();

        ClassificacaoDosBlocosDoCertame.Internos
            .Intersect(ClassificacaoDosBlocosDoCertame.PublicosPorOutroContrato, StringComparer.Ordinal)
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "A classificação não guarda bloco que o envelope deixou de emitir")]
    public void Classificacao_QuandoEnvelopeDeixaDeEmitirBloco_NaoDeveGuardaLo()
    {
        // A lista envelhece nos dois sentidos.
        HashSet<string> emitidos = [
            .. ChavesDoEnvelopeDeReferencia(),
            .. ChavesDe(EnvelopeCanonicoGoldenTests.CanonicalizarReferenciaComCascata()),
            // Condicional da retificação: só existe depois que o edital é emendado, e a
            // canonicalização de referência publica pela primeira vez.
            "retificacao",
        ];

        ClassificacaoDosBlocosDoCertame.Classificados.Except(emitidos, StringComparer.Ordinal)
            .Should().BeEmpty("a classificação descreve blocos que o envelope não emite mais");
    }

    [Fact(DisplayName = "A projeção pública lê o envelope canônico REAL, não só o escrito à mão nos testes")]
    public void Projetar_QuandoEnvelopeCanonicoReal_DeveSerLidoComSucesso()
    {
        // A fronteira acima compara só as chaves de topo: renomear chave interna a mantém verde e
        // faz a leitura pública recusar todo certame.
        JsonObject envelope = (JsonObject)JsonNode.Parse(
            EnvelopeCanonicoGoldenTests.CanonicalizarReferencia().Bytes)!;

        Result<CertamePublicadoDto> resultado = ProjecaoDoCertamePublicado.Projetar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Certame de referência", new string('a', 64), envelope);

        resultado.IsSuccess.Should().BeTrue(
            "o contrato público precisa saber ler o envelope que o canonicalizador de fato emite — recusa: {0}",
            resultado.Error?.Message);
    }

    [Theory(DisplayName = "Documento com bloco presente mas incompleto é recusado, não servido com nulo")]
    [InlineData("tipoProcesso", "{}")]
    [InlineData("documentoEdital", """{"documentoEditalId":"01a0b13f-b9ed-70f4-9ff3-cfdc7503fc9b"}""")]
    [InlineData("localidade", """{"codigoIbge":"1504208"}""")]
    [InlineData("periodo", """{"numero":"001/2026"}""")]
    public void TentarLerProjecao_QuandoBlocoTemMembroFaltando_DeveRecusar(string bloco, string conteudoParcial)
    {
        // Conferir só a presença do bloco deixa passar o bloco vazio: o desserializador constrói o
        // registro com membros nulos, e a resposta sai 200 com campo que o contrato declara
        // obrigatório valendo null. Descer a cada campo de cada tipo aninhado à mão duplicaria a
        // forma do contrato e envelheceria a cada campo novo — quem recusa é a desserialização.
        JsonObject documento = (JsonObject)JsonNode.Parse(DocumentoInteiro())!;
        documento[bloco] = JsonNode.Parse(conteudoParcial);

        ProjecaoDoCertamePublicado.TentarLerProjecao(documento.ToJsonString(), out CertamePublicadoDto? certame)
            .Should().BeFalse("bloco incompleto é meia projeção, e meia projeção mente");

        certame.Should().BeNull();
    }

    [Theory(DisplayName = "Membro obrigatório presente e NULO é recusado, como o ausente")]
    [InlineData("tipoProcesso", """{"codigo":null,"nome":"SISU"}""")]
    [InlineData("localidade", """{"codigoIbge":"1504208","nome":null,"uf":"PA","fusoHorario":"America/Belem"}""")]
    public void TentarLerProjecao_QuandoMembroObrigatorioENulo_DeveRecusar(string bloco, string conteudoComNulo)
    {
        // A exigência de parâmetro de construtor separa ausência de nulo explícito: o argumento
        // existe, então ela aceita. Quem recusa o nulo num membro que o contrato declara
        // não-anulável é a exigência de anotação de nulidade — e sem ela a resposta sai 200 com
        // campo obrigatório valendo null.
        JsonObject documento = (JsonObject)JsonNode.Parse(DocumentoInteiro())!;
        documento[bloco] = JsonNode.Parse(conteudoComNulo);

        ProjecaoDoCertamePublicado.TentarLerProjecao(documento.ToJsonString(), out CertamePublicadoDto? certame)
            .Should().BeFalse();

        certame.Should().BeNull();
    }

    [Fact(DisplayName = "Membro declarado anulável continua aceitando nulo")]
    public void TentarLerProjecao_QuandoMembroAnulavelENulo_DeveAceitar()
    {
        // O limite da exigência: `numero` do período é opcional por contrato, e recusá-lo tornaria
        // indivulgável todo certame cuja publicação não declara número de edital.
        JsonObject documento = (JsonObject)JsonNode.Parse(DocumentoInteiro())!;
        ((JsonObject)documento["periodo"]!)["numero"] = null;

        ProjecaoDoCertamePublicado.TentarLerProjecao(documento.ToJsonString(), out CertamePublicadoDto? certame)
            .Should().BeTrue();

        certame!.Periodo.Numero.Should().BeNull();
    }

    [Fact(DisplayName = "O documento inteiro, como a materialização o grava, é lido com sucesso")]
    public void TentarLerProjecao_QuandoDocumentoInteiro_DeveLer()
    {
        // O outro lado da conferência: uma recusa severa demais tornaria toda divulgação ilegível,
        // e o teste acima passaria com a leitura pública inteiramente quebrada.
        ProjecaoDoCertamePublicado.TentarLerProjecao(DocumentoInteiro(), out CertamePublicadoDto? certame)
            .Should().BeTrue();

        certame.Should().NotBeNull();
    }

    /// <summary>O documento como a divulgação o grava: projetado do envelope canônico real.</summary>
    private static string DocumentoInteiro()
    {
        JsonObject envelope = (JsonObject)JsonNode.Parse(
            EnvelopeCanonicoGoldenTests.CanonicalizarReferencia().Bytes)!;

        Result<CertamePublicadoDto> projecao = ProjecaoDoCertamePublicado.Projetar(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Certame de referência", new string('a', 64), envelope);

        projecao.IsSuccess.Should().BeTrue(projecao.Error?.Message);

        return JsonSerializer.Serialize(projecao.Value!, ProjecaoDoCertamePublicado.OpcoesDoDocumento);
    }

    private static IReadOnlyCollection<string> NaoClassificados(IEnumerable<string> chaves) =>
        [.. chaves.Where(chave => !ClassificacaoDosBlocosDoCertame.Classificados.Contains(chave)).Order(StringComparer.Ordinal)];

    private static IReadOnlyCollection<string> ChavesDoEnvelopeDeReferencia() =>
        ChavesDe(EnvelopeCanonicoGoldenTests.CanonicalizarReferencia());

    private static IReadOnlyCollection<string> ChavesDe(SnapshotCanonico canonico)
    {
        JsonObject envelope = (JsonObject)JsonNode.Parse(canonico.Bytes)!;
        return [.. envelope.Select(static par => par.Key)];
    }
}
