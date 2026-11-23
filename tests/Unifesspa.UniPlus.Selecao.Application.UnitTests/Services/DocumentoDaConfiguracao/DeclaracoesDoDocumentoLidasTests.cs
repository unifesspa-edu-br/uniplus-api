namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A tradução do documento para as declarações do domínio copia valores e posições de origem, sem decidir regra.</summary>
public sealed class DeclaracoesDoDocumentoLidasTests
{
    private const string FasesForaDeOrdem = """
        {
          "cronograma": { "fases": [
            { "chave": "resultado", "tipoFaseCodigo": "RESULTADO" },
            { "chave": "inscricao", "tipoFaseCodigo": "INSCRICAO", "unidadeResponsavelCodigo": "CEPS" },
            { "chave": "recurso", "tipoFaseCodigo": "RECURSO" }
          ] }
        }
        """;

    private const string FaseCompleta = """
        {
          "cronograma": { "fases": [ {
            "chave": "analise", "tipoFaseCodigo": "ANALISE",
            "inicio": "2027-01-10T03:00:00Z", "fim": "2027-01-12T15:30:00.5Z",
            "emiteParecerIndividual": true,
            "tiposDeDecisao": [ "ANALISE_DOCUMENTAL", "HOMOLOGACAO_DAS_INSCRICOES" ],
            "publicacoesPrevistas": [ { "atoCodigo": "AVISO" }, { "atoCodigo": "RESULTADO_PRELIMINAR", "papel": "PRELIMINAR" } ],
            "bancasRequeridas": [
              { "chave": "documental", "tipoBancaCodigo": "BANCA_DOCUMENTAL", "categoriasDocumentoCodigos": [ "RENDA", "IDENTIDADE" ] },
              { "chave": "colegiado", "tipoBancaCodigo": "BANCA_COLEGIADA", "regraDeDeliberacao": { "modo": "colegiadaComVotacao", "integrantes": 3, "suplentes": 1, "quorum": 2,
                "criterio": "maioriaAbsoluta", "desempate": "votoDaPresidencia", "encerramento": "presidenciaNoQuorum" } }
            ]
          } ] }
        }
        """;

    private const string RegraDeRecurso = """
        {
          "cronograma": { "fases": [ {
            "chave": "analise", "tipoFaseCodigo": "ANALISE",
            "regraRecurso": {
              "regraCodigo": "PRAZO-ANCORADO", "regraVersao": "v2", "prazoValor": 2.5, "prazoUnidade": "diasUteis",
              "publicacaoDeReferenciaAtoCodigo": "RESULTADO_PRELIMINAR", "admiteInstanciaSuperior": true,
              "prazoSegundaInstanciaValor": 3, "prazoSegundaInstanciaUnidade": "horas",
              "efeitoSuspensivo": "semEfeitoSuspensivo", "guardaDaVaga": "ateOResultadoDefinitivo",
              "procedimentosDeRecurso": [
                { "tipoDeDecisao": "ANALISE_DOCUMENTAL", "regra": { "codigo": "PROCEDIMENTO-CONFERENCIA", "versao": "v1" } },
                { "tipoDeDecisao": "NOTA_DA_ETAPA", "alvo": { "areaDoEnem": "REDACAO" }, "regra": { "codigo": "PROCEDIMENTO-RECALCULO", "versao": "v1" } },
                { "tipoDeDecisao": "NOTA_DA_ETAPA", "alvo": { "itemDeAvaliacaoChave": "item-de-prova" } }
              ]
            }
          } ] }
        }
        """;

    private const string Homologacao = """
        {
          "cronograma": { "fases": [ {
            "chave": "homologacao", "tipoFaseCodigo": "HOMOLOGACAO",
            "regraDeHomologacao": {
              "resultadoAtoCodigo": "RESULTADO_HOMOLOGACAO",
              "alternativas": [
                { "condicoes": [ { "tipo": "RESULTADO_NO_RETORNO_DO_INEP", "edicaoDoExame": 2025, "motivoDesfavoravelCodigo": "SEM_NOTA" } ] },
                { "condicoes": [ { "tipo": "RESULTADO_NO_RETORNO_DO_INEP", "edicaoDoExame": 2024, "motivoDesfavoravelCodigo": "SEM_NOTA",
                  "enquadramento": [ [ { "fato": "MODALIDADE", "operador": "EM", "valor": [ "AC", "PPI" ] } ] ] } ] }
              ]
            }
          } ] }
        }
        """;

    private static DeclaracoesDoDocumento Lidas(string texto) => DeclaracoesDoDocumentoLidas.De(JsonNode.Parse(texto)!.AsObject());

    private static FaseNoDocumento UnicaFase(string texto) => Lidas(texto).Fases.Should().ContainSingle().Subject;

    [Fact(DisplayName = "A identificação copia o nome e cada código como foi escrito e a origem dos candidatos pelo nome")]
    public void Identificacao_CopiaOsCodigosEAOrigem()
    {
        IdentificacaoNoDocumento identificacao = Lidas("""
            { "identificacao": { "nome": "Processo Seletivo de Exemplo 2027", "tipoProcessoCodigo": "SiSU", "unidadeAdministradoraCodigo": "CEPS", "origemCandidatos": "importacaoExterna",
              "localidadeCodigoIbge": "1504208", "identificadorLegivel": "processo-de-teste", "algoritmoContagemPrazo": { "codigo": "CONTAGEM", "versao": "v1" } } }
            """).Identificacao;

        identificacao.Should().Be(new IdentificacaoNoDocumento(
            "Processo Seletivo de Exemplo 2027", "SiSU", "CEPS", OrigemCandidatos.ImportacaoExterna, "1504208", "processo-de-teste", new("CONTAGEM", "v1")));
    }

    [Fact(DisplayName = "A identificação ausente fica sem código e com a origem dos candidatos sem valor")]
    public void IdentificacaoAusente_FicaSemCodigoESemOrigem() =>
        Lidas("{}").Identificacao.Should().Be(new IdentificacaoNoDocumento(null, null, null, OrigemCandidatos.Nenhuma, null, null, null));

    [Fact(DisplayName = "O documento sem cronograma não tem fase")]
    public void DocumentoSemCronograma_NaoTemFase() => Lidas("{}").Fases.Should().BeEmpty();

    [Fact(DisplayName = "A posição de cada fase é o índice dela na lista enviada, e a chave e os códigos vão como escritos")]
    public void Fases_TemAPosicaoDeOrigemEACodigosCrus()
    {
        IReadOnlyList<FaseNoDocumento> fases = Lidas(FasesForaDeOrdem).Fases;

        fases.Select(static fase => (fase.PosicaoNoDocumento, fase.Chave, fase.Referencias.FaseCanonicaCodigo))
            .Should().Equal((0, "resultado", "RESULTADO"), (1, "inscricao", "INSCRICAO"), (2, "recurso", "RECURSO"));
        fases.Select(static fase => fase.Referencias.UnidadeResponsavelCodigo).Should().Equal(new string?[] { null, "CEPS", null });
    }

    [Fact(DisplayName = "O período da fase usa UTC e mantém ausente o limite não informado")]
    public void Periodo_UsaUtcEMantemAusenteOLimiteNaoInformado()
    {
        FaseNoDocumento completa = UnicaFase(FaseCompleta);
        FaseNoDocumento semDatas = Lidas(FasesForaDeOrdem).Fases[0];

        completa.Janela.Inicio.Should().Be(new DateTimeOffset(2027, 1, 10, 3, 0, 0, TimeSpan.Zero));
        completa.Janela.Fim.Should().Be(new DateTimeOffset(2027, 1, 12, 15, 30, 0, 500, TimeSpan.Zero));
        semDatas.Janela.Inicio.Should().BeNull();
        semDatas.Janela.Fim.Should().BeNull();
    }

    [Fact(DisplayName = "As publicações copiam os produtos com a posição, o papel ausente como nulo e os tipos de decisão como escritos")]
    public void Publicacoes_CopiamProdutosEDecisoes()
    {
        PublicacoesNoDocumento publicacoes = UnicaFase(FaseCompleta).Publicacoes;

        publicacoes.Produtos.Should().Equal(
            new ProdutoNoDocumento(0, "AVISO", null),
            new ProdutoNoDocumento(1, "RESULTADO_PRELIMINAR", PapelProdutoFase.Preliminar));
        publicacoes.EmiteParecerIndividual.Should().BeTrue();
        publicacoes.TiposDeDecisao.Should().Equal("ANALISE_DOCUMENTAL", "HOMOLOGACAO_DAS_INSCRICOES");
    }

    [Fact(DisplayName = "A marca de mesmo momento da fase vai como escrita, e a fase sem a propriedade fica sem marca")]
    public void MarcaDeMesmoMomento_VaiComoEscritaEAusenteFicaFalsa()
    {
        IReadOnlyList<FaseNoDocumento> fases = Lidas("""
            { "cronograma": { "fases": [
              { "chave": "isencao", "tipoFaseCodigo": "ISENCAO" },
              { "chave": "inscricao", "tipoFaseCodigo": "INSCRICAO", "noMesmoMomentoDaAnterior": true },
              { "chave": "resultado", "tipoFaseCodigo": "RESULTADO", "noMesmoMomentoDaAnterior": false }
            ] } }
            """).Fases;

        fases.Select(static fase => fase.NoMesmoMomentoDaAnterior).Should().Equal(false, true, false);
    }

    [Fact(DisplayName = "A fase sem publicações não emite parecer, não tem produto e não declara tipos de decisão")]
    public void FaseSemPublicacoes_TemOValorDePartida()
    {
        PublicacoesNoDocumento publicacoes = Lidas(FasesForaDeOrdem).Fases[0].Publicacoes;

        publicacoes.Produtos.Should().BeEmpty();
        publicacoes.EmiteParecerIndividual.Should().BeFalse();
        publicacoes.TiposDeDecisao.Should().BeNull();
        publicacoes.RegraDeHomologacao.Should().BeNull();
    }

    [Fact(DisplayName = "A banca copia a posição, a chave, o tipo e as categorias dos candidatos atendidos pelo código")]
    public void Banca_CopiaPosicaoChaveTipoECategorias()
    {
        BancaNoDocumento banca = UnicaFase(FaseCompleta).Bancas[0];

        (banca.PosicaoNoDocumento, banca.Chave, banca.TipoBancaCodigo).Should().Be((0, "documental", "BANCA_DOCUMENTAL"));
        banca.CategoriasDocumentoCodigos.Should().Equal("RENDA", "IDENTIDADE");
        banca.RegraDeDeliberacao.Should().BeNull();
    }

    [Fact(DisplayName = "A banca sem candidatos atendidos fica com candidatos atendidos vazios, e a deliberação copia cada valor")]
    public void Banca_SemCandidatosAtendidos_CopiaADeliberacao()
    {
        BancaNoDocumento banca = UnicaFase(FaseCompleta).Bancas[1];

        banca.PosicaoNoDocumento.Should().Be(1);
        banca.TipoBancaCodigo.Should().Be("BANCA_COLEGIADA");
        banca.CategoriasDocumentoCodigos.Should().BeEmpty();
        banca.RegraDeDeliberacao.Should().Be(new Domain.ValueObjects.DeclaracaoDaDeliberacao(
            ModoDeDeliberacao.ColegiadaComVotacao.Codigo(), 3, 1, new(2, CriterioDeVotacao.MaioriaAbsoluta.Codigo(),
            DesempateDaVotacao.VotoDaPresidencia.Codigo(), EncerramentoDaVotacao.PresidenciaNoQuorum.Codigo(), null)));
    }

    [Fact(DisplayName = "A regra de recurso copia a regra, o ato cuja publicação inicia o prazo e os parâmetros, cada código como texto")]
    public void RegraDeRecurso_CopiaARegraOAtoDeReferenciaEOPrazo()
    {
        RegraDeRecursoNoDocumento regra = UnicaFase(RegraDeRecurso).RegraDeRecurso!;

        regra.Regra.Should().Be(new ReferenciaDeRegraNoDocumento("PRAZO-ANCORADO", "v2"));
        regra.AtoAncoraCodigo.Should().Be("RESULTADO_PRELIMINAR");
        regra.Parametros.Should().Be(new ParametrosDeclaradosDoRecurso(
            new PrazoDeclarado(2.5m, "diasUteis"), true, new PrazoDeclarado(3m, "horas"), "semEfeitoSuspensivo", "ateOResultadoDefinitivo"));
    }

    [Fact(DisplayName = "O procedimento de recurso copia a posição, o tipo de decisão, o alvo e a regra, e a regra ausente é referência vazia")]
    public void Procedimentos_CopiamPosicaoTipoAlvoERegra()
    {
        IReadOnlyList<ProcedimentoNoDocumento> procedimentos = UnicaFase(RegraDeRecurso).RegraDeRecurso!.Procedimentos;

        procedimentos.Should().Equal(
            new ProcedimentoNoDocumento(0, TipoMotivoDeDecisao.AnaliseDocumental, null, new("PROCEDIMENTO-CONFERENCIA", "v1")),
            new ProcedimentoNoDocumento(1, TipoMotivoDeDecisao.NotaDaEtapa, new(null, "REDACAO"), new("PROCEDIMENTO-RECALCULO", "v1")),
            new ProcedimentoNoDocumento(2, TipoMotivoDeDecisao.NotaDaEtapa, new("item-de-prova", null), new(null, null)));
    }

    [Fact(DisplayName = "O código de tipo de decisão que o vocabulário não conhece fica ausente no procedimento, para o domínio recusar, sem exceção")]
    public void Procedimento_ComTipoDeDecisaoDesconhecido_FicaSemTipo()
    {
        IReadOnlyList<ProcedimentoNoDocumento> procedimentos = UnicaFase("""
            { "cronograma": { "fases": [ { "chave": "analise", "tipoFaseCodigo": "ANALISE", "regraRecurso": { "regraCodigo": "PRAZO-ANCORADO",
              "procedimentosDeRecurso": [ { "tipoDeDecisao": "TIPO_QUE_NAO_EXISTE", "regra": { "codigo": "PROCEDIMENTO-CONFERENCIA", "versao": "v1" } } ] } } ] } }
            """).RegraDeRecurso!.Procedimentos;

        procedimentos.Should().ContainSingle().Which.TipoDeDecisao.Should().BeNull();
    }

    [Fact(DisplayName = "A regra de recurso sem parâmetros fica sem valor declarado, para o domínio recusar")]
    public void RegraDeRecursoSemParametros_FicaSemValorDeclarado()
    {
        RegraDeRecursoNoDocumento regra = UnicaFase("""
            { "cronograma": { "fases": [ { "chave": "analise", "tipoFaseCodigo": "ANALISE", "regraRecurso": { "regraCodigo": "PRAZO-ANCORADO" } } ] } }
            """).RegraDeRecurso!;

        regra.Parametros.Should().Be(new ParametrosDeclaradosDoRecurso(PrazoDeclarado.Ausente, false, PrazoDeclarado.Ausente, null, null));
        regra.Regra.Should().Be(new ReferenciaDeRegraNoDocumento("PRAZO-ANCORADO", null));
        regra.AtoAncoraCodigo.Should().BeNull();
        regra.Procedimentos.Should().BeEmpty();
    }

    [Fact(DisplayName = "A homologação copia o resultado, as alternativas e as condições, com o enquadramento quando existe")]
    public void Homologacao_CopiaResultadoAlternativasECondicoes()
    {
        RegraDeHomologacaoNoDocumento regra = UnicaFase(Homologacao).Publicacoes.RegraDeHomologacao!;

        regra.ResultadoAtoCodigo.Should().Be("RESULTADO_HOMOLOGACAO");
        CondicaoNoDocumento[] condicoes = [.. regra.Alternativas.Select(static alternativa => alternativa.Condicoes.Single())];
        condicoes.Select(static condicao => (condicao.Tipo, condicao.EdicaoDoExame, condicao.MotivoDesfavoravelCodigo)).Should().Equal(
            (TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep, 2025, "SEM_NOTA"), (TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep, 2024, "SEM_NOTA"));
        condicoes[0].Enquadramento.Should().BeNull();
        condicoes[1].Enquadramento!.Value.GetRawText().Should().Contain("MODALIDADE");
    }

    [Fact(DisplayName = "O enquadramento é copiado como JSON, sem montar o predicado: a recusa dele é do domínio")]
    public void Enquadramento_ECopiadoSemRegra()
    {
        JsonObject documento = JsonNode.Parse(Homologacao)!.AsObject();
        JsonObject condicao = documento["cronograma"]!["fases"]![0]!["regraDeHomologacao"]!["alternativas"]![1]!["condicoes"]![0]!.AsObject();
        condicao["enquadramento"] = JsonNode.Parse("""[ [ { "fato": "MODALIDADE", "operador": "IGUAL", "valor": [ "AC" ] } ] ]""");

        DeclaracoesDoDocumento lidas = DeclaracoesDoDocumentoLidas.De(documento);

        lidas.Fases.Single().Publicacoes.RegraDeHomologacao!.Alternativas[1].Condicoes.Single().Enquadramento!.Value.GetRawText().Should().Contain("\"IGUAL\"");
    }

    [Fact(DisplayName = "O documento nulo é defeito de quem chama")]
    public void DocumentoNulo_Lanca()
    {
        Action ler = () => DeclaracoesDoDocumentoLidas.De(null!);

        ler.Should().Throw<ArgumentNullException>();
    }
}
