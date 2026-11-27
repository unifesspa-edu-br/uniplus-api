namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

/// <summary>
/// As vagas do documento são copiadas com a posição que cada item tinha na lista enviada e cada código como foi escrito; o que só é
/// de leitura (o quadro calculado, os nomes) não entra, e o valor de tipo errado fica ausente, sem exceção.
/// </summary>
public sealed class VagasDoDocumentoLidasTests
{
    private const string DocumentoCompleto = """
        {
          "opcoesDeCurso": { "numero": 2 },
          "distribuicaoVagas": [
            { "ofertaCursoCodigo": "OFERTA_B", "vagasOfertadasBase": 40, "percentualDaReservaLegal": 50.5,
              "regraDistribuicao": { "codigo": "DISTRIBUICAO_LEI_DE_COTAS", "versao": "v1" },
              "regraAjuste": { "codigo": "AJUSTE_ARTIGO_11", "versao": "v2" },
              "ajusteDoQuadro": { "$tipo": "reduzirProporcionalEm", "modalidadeCodigos": [ "COTA_A", "COTA_B" ] },
              "referenciaDemograficaCodigo": "CENSO_2022",
              "modalidades": [ { "codigo": "COTA_B", "quantidadeDeclarada": 10, "descricao": "só leitura" }, { "codigo": "AMPLA" } ],
              "cursoNome": "só leitura", "turnos": [ "NOTURNO" ], "totalDeVagasDoEdital": 99,
              "quadro": [ { "modalidadeCodigo": "AMPLA", "quantidade": 99 } ] },
            { "ofertaCursoCodigo": "OFERTA_A", "ajusteDoQuadro": { "$tipo": "reduzirDe", "modalidadeCodigo": "COTA_A" }, "referenciaDemograficaCodigo": null }
          ],
          "ordemDeRemanejamento": {
            "regra": { "codigo": "CRITERIO_FEDERAL", "versao": "v1" }, "destinoFinalCodigo": "AMPLA",
            "destinos": [
              { "modalidadeOrigemCodigo": "COTA_B", "modalidadeDestinoCodigo": "COTA_A" },
              { "modalidadeOrigemCodigo": "COTA_B", "modalidadeDestinoCodigo": "AMPLA" }
            ]
          },
          "fada": { "aplica": true, "regra": { "codigo": "FADA_MULTIPLICATIVO", "versao": "v1" }, "fator": 1.1, "teto": 1.5,
                    "codigoDoDadoDeElegibilidade": "PROFESSOR_RURAL", "baseLegalCodigo": "PORTARIA_2514", "identificacao": "só leitura" },
          "atendimento": {
            "condicoes": [ { "condicaoCodigo": "PCD", "condicaoNome": "só leitura" }, { "condicaoCodigo": "TEA" } ],
            "recursos": [ { "recursoCodigo": "LIBRAS" } ],
            "tiposDeficiencia": [ { "tipoDeficienciaCodigo": "VISUAL" }, { "tipoDeficienciaCodigo": "AUDITIVA" } ]
          }
        }
        """;

    private static VagasNoDocumento Lidas(string documento) => VagasDoDocumentoLidas.De(JsonNode.Parse(documento)!.AsObject());

    [Fact(DisplayName = "O documento sem nenhum bloco de vagas não declara opções, distribuição, cascata, FADA nem atendimento")]
    public void SemBlocos_NaoDeclaraNada()
    {
        VagasNoDocumento vagas = Lidas("{}");

        vagas.OpcoesDeCurso.Should().BeNull();
        vagas.Distribuicoes.Should().BeEmpty();
        vagas.Cascata.Should().BeNull();
        vagas.Fada.Should().BeNull();
        vagas.Atendimento.Should().BeNull();
    }

    [Fact(DisplayName = "A distribuição guarda a posição na lista enviada, a oferta e os códigos como escritos, mesmo fora de ordem")]
    public void Distribuicao_TemAPosicaoEOsCodigosComoEscritos()
    {
        VagasNoDocumento vagas = Lidas(DocumentoCompleto);

        vagas.OpcoesDeCurso.Should().Be(2);
        vagas.Distribuicoes.Select(static d => (d.PosicaoNoDocumento, d.OfertaCursoCodigo)).Should().Equal((0, "OFERTA_B"), (1, "OFERTA_A"));
        DistribuicaoNoDocumento primeira = vagas.Distribuicoes[0];
        primeira.VagasOfertadasBase.Should().Be(40);
        primeira.PercentualDaReservaLegal.Should().Be(50.5m);
        primeira.RegraDeDistribuicao.Should().Be(new ReferenciaDeRegraNoDocumento("DISTRIBUICAO_LEI_DE_COTAS", "v1"));
        primeira.RegraDeAjuste.Should().Be(new ReferenciaDeRegraNoDocumento("AJUSTE_ARTIGO_11", "v2"));
        primeira.ReferenciaDemograficaCodigo.Should().Be("CENSO_2022");
        primeira.AjusteDoQuadro!.Tipo.Should().Be("reduzirProporcionalEm");
        primeira.AjusteDoQuadro.ModalidadeCodigos.Should().Equal("COTA_A", "COTA_B");
    }

    [Fact(DisplayName = "A modalidade guarda a posição e a quantidade declarada, e a que não declara quantidade fica sem ela")]
    public void Modalidade_TemAPosicaoEAQuantidadeDeclarada()
    {
        DistribuicaoNoDocumento primeira = Lidas(DocumentoCompleto).Distribuicoes[0];

        primeira.Modalidades.Should().Equal(new ModalidadeNoDocumento(0, "COTA_B", 10), new ModalidadeNoDocumento(1, "AMPLA", null));
    }

    [Fact(DisplayName = "A distribuição que traz só a oferta deixa o resto ausente, e o ajuste de uma modalidade guarda só ela")]
    public void Distribuicao_MinimaDeixaORestoAusente()
    {
        DistribuicaoNoDocumento segunda = Lidas(DocumentoCompleto).Distribuicoes[1];

        segunda.VagasOfertadasBase.Should().BeNull();
        segunda.PercentualDaReservaLegal.Should().BeNull();
        segunda.RegraDeDistribuicao.Should().BeNull();
        segunda.ReferenciaDemograficaCodigo.Should().BeNull();
        segunda.Modalidades.Should().BeEmpty();
        segunda.AjusteDoQuadro.Should().Be(new AjusteDoQuadroNoDocumento("reduzirDe", "COTA_A", []));
    }

    [Fact(DisplayName = "A cascata guarda a regra, o destino final e cada destino com a posição, sem ordem escrita")]
    public void Cascata_TemOsDestinosComPosicao()
    {
        CascataNoDocumento cascata = Lidas(DocumentoCompleto).Cascata!;

        cascata.Regra.Should().Be(new ReferenciaDeRegraNoDocumento("CRITERIO_FEDERAL", "v1"));
        cascata.DestinoFinalCodigo.Should().Be("AMPLA");
        cascata.Destinos.Should().Equal(new DestinoDaCascataNoDocumento(0, "COTA_B", "COTA_A"), new DestinoDaCascataNoDocumento(1, "COTA_B", "AMPLA"));
        cascata.DestinosComOrdem().Select(static par => par.Ordem).Should().Equal(1, 2);
    }

    [Fact(DisplayName = "O FADA guarda se aplica, a regra, o fator, o teto, o dado de elegibilidade e a base legal pelo código")]
    public void Fada_TemOsValoresEscritos()
    {
        FadaNoDocumento fada = Lidas(DocumentoCompleto).Fada!;

        fada.Should().Be(new FadaNoDocumento(
            true, new("FADA_MULTIPLICATIVO", "v1"), 1.1m, 1.5m, "PROFESSOR_RURAL", "PORTARIA_2514"));
    }

    [Fact(DisplayName = "O atendimento guarda os códigos das três listas com a posição de cada um, sem os nomes só de leitura")]
    public void Atendimento_TemOsCodigosComPosicao()
    {
        AtendimentoNoDocumento atendimento = Lidas(DocumentoCompleto).Atendimento!;

        atendimento.Condicoes.Should().Equal(new CodigoNoDocumento(0, "PCD"), new CodigoNoDocumento(1, "TEA"));
        atendimento.Recursos.Should().Equal(new CodigoNoDocumento(0, "LIBRAS"));
        atendimento.TiposDeficiencia.Should().Equal(new CodigoNoDocumento(0, "VISUAL"), new CodigoNoDocumento(1, "AUDITIVA"));
    }

    [Fact(DisplayName = "O valor de tipo errado fica ausente ou no valor de partida, sem exceção")]
    public void TipoErrado_FicaAusente()
    {
        VagasNoDocumento vagas = Lidas("""
            {
              "opcoesDeCurso": { "numero": "dois" },
              "distribuicaoVagas": [ { "ofertaCursoCodigo": 7, "vagasOfertadasBase": "muitas", "percentualDaReservaLegal": "metade",
                                       "modalidades": [ { "codigo": 1, "quantidadeDeclarada": 2.5 } ], "ajusteDoQuadro": { "$tipo": 3 } } ],
              "ordemDeRemanejamento": { "regra": { "codigo": 1 }, "destinoFinalCodigo": false, "destinos": [ { "modalidadeOrigemCodigo": 9 } ] },
              "fada": { "aplica": "sim", "fator": "alto", "teto": true },
              "atendimento": { "condicoes": [ { "condicaoCodigo": 4 } ] }
            }
            """);

        vagas.OpcoesDeCurso.Should().BeNull();
        DistribuicaoNoDocumento distribuicao = vagas.Distribuicoes.Should().ContainSingle().Subject;
        distribuicao.OfertaCursoCodigo.Should().BeNull();
        distribuicao.VagasOfertadasBase.Should().BeNull();
        distribuicao.PercentualDaReservaLegal.Should().BeNull();
        distribuicao.Modalidades.Should().ContainSingle().Which.Should().Be(new ModalidadeNoDocumento(0, null, null));
        distribuicao.AjusteDoQuadro!.Tipo.Should().BeNull();
        vagas.Cascata!.DestinoFinalCodigo.Should().BeNull();
        vagas.Cascata.Destinos.Should().ContainSingle().Which.ModalidadeOrigemCodigo.Should().BeNull();
        vagas.Fada.Should().Be(new FadaNoDocumento(false, null, null, null, null, null));
        vagas.Atendimento!.Condicoes.Should().ContainSingle().Which.Codigo.Should().BeNull();
    }
}
