namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

/// <summary>
/// A classificação do documento é copiada com as regras pelo código e pela versão, cada eliminação e cada desempate na posição
/// enviada, e o que falta como nulo ou lista vazia; o quadro de pesos por área, só de leitura, não é lido.
/// </summary>
public sealed class ClassificacaoDoDocumentoLidaTests
{
    private const string Completa = """
        {
          "regraCalculo": { "codigo": "CALCULO_MEDIA_PONDERADA", "versao": "v1" },
          "regraArredondamento": { "codigo": "ARREDONDAMENTO_PARA_CIMA", "versao": "v2" },
          "casasArredondamento": 3, "escalaNotaFinal": 1000.5,
          "regraOrdemAlocacao": { "codigo": "ORDEM_POR_NOTA", "versao": "v1" },
          "baseadoEmEnem": true, "resolucaoPesoAreaEnem": "RESOLUCAO_2027",
          "quadroPesoAreaEnem": [ { "grupoCurso": "G1" } ],
          "regrasEliminacao": [
            { "regra": { "codigo": "ELIMINACAO_NOTA_MINIMA_ETAPA", "versao": "v1" }, "parametros": { "etapaChave": "redacao", "notaMinima": 5.5 } },
            { "regra": { "codigo": "ELIMINACAO_CORTE_EM_AREA", "versao": "v1" }, "parametros": { "areaCodigo": "MATEMATICA", "minimo": 300 } },
            { "regra": { "codigo": "ELIMINACAO_ZERO_EM_AREA", "versao": "v1" } }
          ],
          "criteriosDeDesempate": [
            { "ordem": 7, "regra": { "codigo": "DESEMPATE_MAIOR_NOTA_ETAPA", "versao": "v1" }, "parametros": { "etapaChave": "redacao" } },
            { "regra": { "codigo": "DESEMPATE_IDOSO", "versao": "v1" }, "parametros": { "idadeMinima": 60 } },
            { "regra": { "codigo": "DESEMPATE_MAIOR_NOTA_AREA_ENEM", "versao": "v1" }, "parametros": { "areas": [ "MATEMATICA", "LINGUAGENS" ] } },
            { "regra": { "codigo": "DESEMPATE_MAIOR_IDADE", "versao": "v1" } },
            { }
          ]
        }
        """;

    private static ClassificacaoNoDocumento? Lida(string classificacao) => ClassificacaoDoDocumentoLida.De(JsonNode.Parse(classificacao));

    private static ClassificacaoNoDocumento LidaCompleta() => Lida(Completa)!;

    [Fact(DisplayName = "O bloco ausente é nulo, e o bloco vazio é uma classificação sem regra, sem eliminação e sem desempate")]
    public void BlocoAusente_ENulo_EVazioNaoTemNada()
    {
        ClassificacaoDoDocumentoLida.De(null).Should().BeNull();
        ClassificacaoNoDocumento vazia = Lida("{}")!;

        vazia.Should().Match<ClassificacaoNoDocumento>(c =>
            c.RegraCalculo == null && c.RegraArredondamento == null && c.RegraOrdemAlocacao == null && c.CasasArredondamento == null
            && c.EscalaNotaFinal == null && c.BaseadoEmEnem == null && c.ResolucaoPesoAreaEnem == null);
        vazia.RegrasEliminacao.Should().BeEmpty();
        vazia.CriteriosDeDesempate.Should().BeEmpty();
    }

    [Fact(DisplayName = "As regras de cálculo, arredondamento e ordem de alocação são copiadas pelo código e pela versão")]
    public void Regras_SaoCopiadasPeloCodigoEPelaVersao()
    {
        ClassificacaoNoDocumento classificacao = LidaCompleta();

        classificacao.RegraCalculo.Should().Be(new ReferenciaDeRegraNoDocumento("CALCULO_MEDIA_PONDERADA", "v1"));
        classificacao.RegraArredondamento.Should().Be(new ReferenciaDeRegraNoDocumento("ARREDONDAMENTO_PARA_CIMA", "v2"));
        classificacao.RegraOrdemAlocacao.Should().Be(new ReferenciaDeRegraNoDocumento("ORDEM_POR_NOTA", "v1"));
    }

    [Fact(DisplayName = "As casas, a escala da nota final, a marca do ENEM e a resolução dos pesos são copiadas como escritas")]
    public void CamposSimples_SaoCopiados()
    {
        ClassificacaoNoDocumento classificacao = LidaCompleta();

        (classificacao.CasasArredondamento, classificacao.EscalaNotaFinal, classificacao.BaseadoEmEnem, classificacao.ResolucaoPesoAreaEnem)
            .Should().Be((3, 1000.5m, true, "RESOLUCAO_2027"));
    }

    [Fact(DisplayName = "A regra de eliminação copia a posição e só os parâmetros do código dela: a etapa e a nota mínima, ou a área e o mínimo")]
    public void Eliminacoes_CopiamPosicaoEParametrosDoCodigo()
    {
        LidaCompleta().RegrasEliminacao.Should().Equal(
            new RegraDeEliminacaoNoDocumento(0, new("ELIMINACAO_NOTA_MINIMA_ETAPA", "v1"), "redacao", null, 5.5m, null),
            new RegraDeEliminacaoNoDocumento(1, new("ELIMINACAO_CORTE_EM_AREA", "v1"), null, "MATEMATICA", null, 300m),
            new RegraDeEliminacaoNoDocumento(2, new("ELIMINACAO_ZERO_EM_AREA", "v1"), null, null, null, null));
    }

    [Fact(DisplayName = "O critério de desempate copia a posição, a regra e os parâmetros, e a ordem escrita no documento é ignorada")]
    public void Desempates_CopiamPosicaoRegraEParametros()
    {
        IReadOnlyList<CriterioDeDesempateNoDocumento> criterios = LidaCompleta().CriteriosDeDesempate;

        criterios.Select(static criterio => (criterio.PosicaoNoDocumento, criterio.Regra.Codigo)).Should().Equal(
            (0, "DESEMPATE_MAIOR_NOTA_ETAPA"), (1, "DESEMPATE_IDOSO"), (2, "DESEMPATE_MAIOR_NOTA_AREA_ENEM"), (3, "DESEMPATE_MAIOR_IDADE"), (4, null));
        criterios[0].Parametros.Should().BeEquivalentTo(new ParametrosDoDesempateNoDocumento("redacao", null, []));
        criterios[1].Parametros.Should().BeEquivalentTo(new ParametrosDoDesempateNoDocumento(null, 60, []));
        criterios[2].Parametros.Areas.Should().Equal("MATEMATICA", "LINGUAGENS");
    }

    [Fact(DisplayName = "O critério sem parâmetros fica com parâmetros vazios, e o sem regra fica com a referência vazia, para o domínio recusar")]
    public void Desempate_SemParametrosOuSemRegra_FicaVazio()
    {
        IReadOnlyList<CriterioDeDesempateNoDocumento> criterios = LidaCompleta().CriteriosDeDesempate;

        criterios[3].Parametros.Should().BeEquivalentTo(new ParametrosDoDesempateNoDocumento(null, null, []));
        criterios[4].Regra.Should().Be(new ReferenciaDeRegraNoDocumento(null, null));
    }

    [Fact(DisplayName = "A condição sobre dado do critério é copiada como escrita, com o valor em JSON, sem conferir o dado nem o operador")]
    public void Desempate_CondicaoSobreDado_ECopiadaComoEscrita()
    {
        ClassificacaoNoDocumento classificacao = Lida("""
            {
              "criteriosDeDesempate": [
                { "regra": { "codigo": "DESEMPATE_CONDICAO_SOBRE_DADO", "versao": "v1" }, "parametros": { "codigoDoDado": "DADO_QUALQUER", "operador": "ALGUM_DE", "valor": [ "A", "B" ] } },
                { "regra": { "codigo": "DESEMPATE_CONDICAO_SOBRE_DADO", "versao": "v1" }, "parametros": { "codigoDoDado": "PROFESSOR_RURAL" } }
              ]
            }
            """)!;

        ParametrosDoDesempateNoDocumento completa = classificacao.CriteriosDeDesempate[0].Parametros;
        (completa.CodigoDoDado, completa.Operador, completa.Valor!.Value.GetRawText()).Should().Be(("DADO_QUALQUER", "ALGUM_DE", """["A","B"]"""));
        ParametrosDoDesempateNoDocumento incompleta = classificacao.CriteriosDeDesempate[1].Parametros;
        (incompleta.CodigoDoDado, incompleta.Operador, incompleta.Valor).Should().Be(("PROFESSOR_RURAL", null, null));
    }

    [Fact(DisplayName = "Os parâmetros de uma regra desconhecida com tipos que não cabem ficam ausentes, sem exceção")]
    public void ParametrosDeRegraDesconhecida_ComTipoQueNaoCabe_FicamAusentes()
    {
        ClassificacaoNoDocumento classificacao = Lida("""
            { "criteriosDeDesempate": [
              { "regra": { "codigo": "REGRA_NOVA", "versao": "v1" }, "parametros": { "etapaChave": 7, "codigoDoDado": 123, "operador": false, "areas": [ 1, null, "MATEMATICA" ] } } ] }
            """)!;

        ParametrosDoDesempateNoDocumento parametros = classificacao.CriteriosDeDesempate.Single().Parametros;

        (parametros.EtapaChave, parametros.CodigoDoDado, parametros.Operador).Should().Be((null, null, null));
        parametros.Areas.Should().Equal("", "", "MATEMATICA");
    }

    [Fact(DisplayName = "As declarações do documento trazem a classificação do bloco, e o documento sem o bloco a traz nula")]
    public void Declaracoes_TrazemAClassificacaoDoBloco()
    {
        JsonObject documento = JsonNode.Parse("""{ "classificacao": { "casasArredondamento": 2 } }""")!.AsObject();

        DeclaracoesDoDocumentoLidas.De(documento).Classificacao!.CasasArredondamento.Should().Be(2);
        DeclaracoesDoDocumentoLidas.De(JsonNode.Parse("{}")!.AsObject()).Classificacao.Should().BeNull();
    }
}
