namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As etapas do documento são copiadas com a posição e a chave que o autor deu, cada código como foi escrito e cada campo ausente
/// como nulo ou lista vazia, sem decidir regra; a posição é o índice na lista enviada, mesmo fora de ordem.
/// </summary>
public sealed class EtapasDoDocumentoLidasTests
{
    private const string EtapaCompleta = """
        [ {
          "chave": "redacao", "nome": "Redação", "carater": "AMBAS", "tipoEtapaCodigo": "PROVA_DE_REDACAO",
          "peso": 2.5, "notaMinima": 4, "notaMaxima": 10, "passoDoLancamento": 0.5, "formaDeAvaliacao": "HUMANA",
          "regraDeApuracaoDaAvaliacao": { "codigo": "APURACAO_POR_MEDIA", "versao": "v1" },
          "itensDeAvaliacao": [ { "chave": "coesao", "rotulo": "Coesão", "pontuacaoMaxima": 5 }, { "chave": "argumentacao" } ],
          "escalaDeAtendimento": [ { "chave": "parcial", "rotulo": "Parcial", "fracao": 0.5 }, { "chave": "pleno", "rotulo": "Pleno", "fracao": 1 } ],
          "faseChave": "prova", "inicio": "2027-02-01T12:00:00Z", "fim": "2027-02-02T12:00:00.5Z",
          "emiteParecerIndividual": true, "tiposDeDecisao": [ "NOTA_DA_ETAPA" ],
          "publicacoesPrevistas": [ { "atoCodigo": "AVISO" }, { "atoCodigo": "RESULTADO_PRELIMINAR", "papel": "PRELIMINAR" } ],
          "bancas": [
            { "chave": "banca_a", "tipoBancaCodigo": "BANCA_COLEGIADA", "consequenciaDaAusencia": "ELIMINA_DO_PROCESSO",
              "regraDeDeliberacao": { "modo": "COLEGIADA_COM_VOTACAO", "integrantes": 3, "suplentes": 1, "quorum": 2, "criterio": "MAIORIA_ABSOLUTA" } },
            { "chave": "banca_b", "tipoBancaCodigo": "BANCA_DOCUMENTAL" }
          ],
          "recursos": [
            { "eventoInicialDoPrazo": "PUBLICACAO_DO_ATO", "regraCodigo": "PRAZO-ANCORADO", "regraVersao": "v1", "prazoValor": 2, "prazoUnidade": "DIAS_UTEIS",
              "publicacaoDeReferenciaAtoCodigo": "RESULTADO_PRELIMINAR", "efeitoSuspensivo": "SEM_EFEITO_SUSPENSIVO",
              "procedimentosDeRecurso": [ { "tipoDeDecisao": "NOTA_DA_ETAPA", "alvo": { "itemDeAvaliacaoChave": "coesao" }, "regra": { "codigo": "PROCEDIMENTO-RECALCULO", "versao": "v1" } } ] },
            { "eventoInicialDoPrazo": "CIENCIA_INDIVIDUAL" },
            { }
          ]
        } ]
        """;

    private static IReadOnlyList<EtapaNoDocumento> Lidas(string etapas) => EtapasDoDocumentoLidas.De(JsonNode.Parse(etapas));

    private static EtapaNoDocumento Completa() => Lidas(EtapaCompleta).Should().ContainSingle().Subject;

    [Fact(DisplayName = "O documento sem a lista de etapas não tem etapa")]
    public void ListaAusente_NaoTemEtapa()
    {
        EtapasDoDocumentoLidas.De(null).Should().BeEmpty();
        Lidas("[]").Should().BeEmpty();
    }

    [Fact(DisplayName = "A posição de cada etapa é o índice dela na lista enviada, e a chave vai como escrita, mesmo fora de ordem")]
    public void Etapas_TemAPosicaoDeOrigemEAChaveDoAutor()
    {
        IReadOnlyList<EtapaNoDocumento> etapas = Lidas("""[ { "chave": "segunda" }, { "chave": "primeira" }, { "chave": "terceira" } ]""");

        etapas.Select(static etapa => (etapa.PosicaoNoDocumento, etapa.Chave)).Should().Equal((0, "segunda"), (1, "primeira"), (2, "terceira"));
    }

    [Fact(DisplayName = "A identificação copia o nome, o tipo de etapa, a fase pela chave, o caráter e os números como escritos")]
    public void Identificacao_CopiaCadaCampo()
    {
        Completa().Identificacao.Should().Be(new IdentificacaoDaEtapaNoDocumento(
            "Redação", "PROVA_DE_REDACAO", "prova", CaraterEtapa.Ambas, 2.5m, 4m));
    }

    [Fact(DisplayName = "A avaliação copia a regra de apuração, a forma, as notas e o passo do lançamento")]
    public void Avaliacao_CopiaRegraFormaNotasEPasso()
    {
        AvaliacaoDaEtapaNoDocumento avaliacao = Completa().Avaliacao;

        avaliacao.RegraDeApuracao.Should().Be(new ReferenciaDeRegraNoDocumento("APURACAO_POR_MEDIA", "v1"));
        (avaliacao.Forma, avaliacao.NotaMaxima, avaliacao.PassoDoLancamento).Should().Be((FormaDeAvaliacao.Humana, 10m, 0.5m));
    }

    [Fact(DisplayName = "Os itens de avaliação conservam a posição e a chave do autor, e o rótulo ou a pontuação ausentes ficam nulos")]
    public void Itens_ConservamPosicaoEChave()
    {
        Completa().Avaliacao.Itens.Should().Equal(
            new ItemDeAvaliacaoNoDocumento(0, "coesao", "Coesão", 5m),
            new ItemDeAvaliacaoNoDocumento(1, "argumentacao", null, null));
    }

    [Fact(DisplayName = "Os níveis da escala de atendimento conservam a posição e a chave do autor")]
    public void Niveis_ConservamPosicaoEChave()
    {
        Completa().Avaliacao.Niveis.Should().Equal(
            new NivelDeAtendimentoNoDocumento(0, "parcial", "Parcial", 0.5m),
            new NivelDeAtendimentoNoDocumento(1, "pleno", "Pleno", 1m));
    }

    [Fact(DisplayName = "A janela própria da etapa usa UTC, e a etapa sem datas fica com a janela vazia, que segue a da fase")]
    public void Janela_UsaUtcEFicaVaziaSemDatas()
    {
        Completa().Janela.Should().Be(new JanelaDeTempo(new(2027, 2, 1, 12, 0, 0, TimeSpan.Zero), new(2027, 2, 2, 12, 0, 0, 500, TimeSpan.Zero)));
        Lidas("""[ { "chave": "sem_datas" } ]""").Single().Janela.Should().Be(new JanelaDeTempo(null, null));
    }

    [Fact(DisplayName = "As publicações previstas copiam o ato com a posição e o papel, o parecer individual e os tipos de decisão")]
    public void Publicacoes_CopiamAtosParecerEDecisoes()
    {
        EtapaNoDocumento etapa = Completa();

        etapa.PublicacoesPrevistas.Should().Equal(
            new ProdutoNoDocumento(0, "AVISO", null),
            new ProdutoNoDocumento(1, "RESULTADO_PRELIMINAR", PapelDaPublicacaoPrevista.Preliminar));
        etapa.EmiteParecerIndividual.Should().BeTrue();
        etapa.TiposDeDecisao.Should().Equal("NOTA_DA_ETAPA");
    }

    [Fact(DisplayName = "A banca da etapa copia a posição, a chave, o tipo, a deliberação e a consequência da ausência, sem categorias de candidatos")]
    public void Bancas_CopiamPosicaoChaveTipoEDeliberacao()
    {
        IReadOnlyList<BancaNoDocumento> bancas = Completa().Bancas;

        bancas.Select(static banca => (banca.PosicaoNoDocumento, banca.Chave, banca.TipoBancaCodigo, banca.ConsequenciaDaAusencia))
            .Should().Equal((0, "banca_a", "BANCA_COLEGIADA", "ELIMINA_DO_PROCESSO"), (1, "banca_b", "BANCA_DOCUMENTAL", null));
        bancas.Should().AllSatisfy(static banca => banca.CategoriasDocumentoCodigos.Should().BeEmpty());
        bancas[0].RegraDeDeliberacao.Should().NotBeNull();
        bancas[1].RegraDeDeliberacao.Should().BeNull();
    }

    [Fact(DisplayName = "O recurso da etapa copia a posição, o evento que inicia o prazo, a regra, o ato de referência, os parâmetros e os procedimentos")]
    public void Recurso_CopiaEventoRegraAtoEProcedimentos()
    {
        RecursoDaEtapaNoDocumento recurso = Completa().Recursos[0];

        (recurso.PosicaoNoDocumento, recurso.EventoInicialDoPrazo).Should().Be((0, EventoInicialDoPrazo.PublicacaoDoAto));
        recurso.Regra.Regra.Should().Be(new ReferenciaDeRegraNoDocumento("PRAZO-ANCORADO", "v1"));
        recurso.Regra.AtoAncoraCodigo.Should().Be("RESULTADO_PRELIMINAR");
        recurso.Regra.Parametros.PrazoDeInterposicao.Should().Be(new PrazoDeclarado(2m, "DIAS_UTEIS"));
        recurso.Regra.Parametros.EfeitoSuspensivo.Should().Be("SEM_EFEITO_SUSPENSIVO");
        recurso.Regra.Procedimentos.Should().Equal(
            new ProcedimentoNoDocumento(0, TipoMotivoDeDecisao.NotaDaEtapa, new("coesao", null), new("PROCEDIMENTO-RECALCULO", "v1")));
    }

    [Fact(DisplayName = "O recurso só com o evento, ou sem nenhum campo, fica com o evento ou a regra ausentes, para o domínio recusar")]
    public void Recurso_Incompleto_FicaComOQueFalta()
    {
        IReadOnlyList<RecursoDaEtapaNoDocumento> recursos = Completa().Recursos;

        recursos.Select(static recurso => (recurso.PosicaoNoDocumento, recurso.EventoInicialDoPrazo)).Should().Equal(
            (0, EventoInicialDoPrazo.PublicacaoDoAto), (1, EventoInicialDoPrazo.CienciaIndividual), (2, null));
        recursos[2].Regra.Regra.Should().Be(new ReferenciaDeRegraNoDocumento(null, null));
        recursos[2].Regra.Parametros.Should().Be(new ParametrosDeclaradosDoRecurso(PrazoDeclarado.Ausente, false, PrazoDeclarado.Ausente, null, null));
    }

    [Fact(DisplayName = "A etapa só com a chave fica com tudo ausente: nulos, listas vazias e sem marca de parecer individual")]
    public void EtapaSoComAChave_FicaComTudoAusente()
    {
        EtapaNoDocumento etapa = Lidas("""[ { "chave": "so_a_chave" } ]""").Single();

        etapa.Identificacao.Should().Be(new IdentificacaoDaEtapaNoDocumento(null, null, null, null, null, null));
        (etapa.Avaliacao.RegraDeApuracao, etapa.Avaliacao.Forma, etapa.Avaliacao.NotaMaxima, etapa.Avaliacao.PassoDoLancamento).Should().Be((null, null, null, null));
        new[] { etapa.Avaliacao.Itens.Count, etapa.Avaliacao.Niveis.Count, etapa.Bancas.Count, etapa.PublicacoesPrevistas.Count, etapa.Recursos.Count }
            .Should().AllBeEquivalentTo(0);
        etapa.EmiteParecerIndividual.Should().BeFalse();
        etapa.TiposDeDecisao.Should().BeNull();
    }

    [Fact(DisplayName = "O código de caráter, de forma ou de evento que o domínio não conhece fica ausente, sem exceção")]
    public void CodigoDesconhecido_FicaAusente()
    {
        EtapaNoDocumento etapa = Lidas("""
            [ { "chave": "x", "carater": "OUTRO", "formaDeAvaliacao": "OUTRA", "recursos": [ { "eventoInicialDoPrazo": "OUTRO_EVENTO" } ] } ]
            """).Single();

        (etapa.Identificacao.Carater, etapa.Avaliacao.Forma, etapa.Recursos[0].EventoInicialDoPrazo).Should().Be((null, null, null));
    }

    [Fact(DisplayName = "As declarações do documento trazem as etapas na posição enviada, e o documento sem etapas as traz vazias")]
    public void Declaracoes_TrazemAsEtapasDoBloco()
    {
        JsonObject documento = JsonNode.Parse("""{ "etapas": [ { "chave": "a" }, { "chave": "b" } ] }""")!.AsObject();

        DeclaracoesDoDocumentoLidas.De(documento).Etapas.Select(static etapa => etapa.Chave).Should().Equal("a", "b");
        DeclaracoesDoDocumentoLidas.De(JsonNode.Parse("{}")!.AsObject()).Etapas.Should().BeEmpty();
    }
}
