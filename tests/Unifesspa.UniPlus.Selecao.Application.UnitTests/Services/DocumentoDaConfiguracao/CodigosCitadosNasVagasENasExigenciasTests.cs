namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

/// <summary>
/// Os códigos que as vagas, o atendimento, o FADA e as exigências citam saem sem repetição e na ordem em que aparecem; o código
/// ausente não conta, e a regra citada sem código ou sem versão não consulta o catálogo.
/// </summary>
public sealed class CodigosCitadosNasVagasENasExigenciasTests
{
    private static readonly IdentificacaoNoDocumento Identificacao = new(null, null, null, null, null, null, null);

    private static DistribuicaoNoDocumento Distribuicao(
        int posicao, string? oferta, string? referencia = null, ReferenciaDeRegraNoDocumento? regra = null, ReferenciaDeRegraNoDocumento? ajuste = null, params string?[] modalidades) =>
        new(posicao, oferta, null, [.. modalidades.Select(static (codigo, indice) => new ModalidadeNoDocumento(indice, codigo, null))])
        {
            ReferenciaDemograficaCodigo = referencia,
            RegraDeDistribuicao = regra,
            RegraDeAjuste = ajuste,
        };

    private static CodigosDasVagasEDasExigenciasCitados Codigos(VagasNoDocumento? vagas, ExigenciasNoDocumento? exigencias = null) =>
        CodigosCitadosNasVagasENasExigencias.Em(new DeclaracoesDoDocumento(Identificacao, []) { Vagas = vagas, Exigencias = exigencias });

    [Fact(DisplayName = "O documento sem vagas nem exigências não cita código algum")]
    public void SemVagasNemExigencias_NaoCitaCodigo()
    {
        CodigosDasVagasEDasExigenciasCitados codigos = Codigos(null);

        codigos.OfertasDeCurso.Should().BeEmpty();
        codigos.Modalidades.Should().BeEmpty();
        codigos.ReferenciasDemograficas.Should().BeEmpty();
        codigos.CondicoesDeAtendimento.Should().BeEmpty();
        codigos.RecursosDeAcessibilidade.Should().BeEmpty();
        codigos.TiposDeDeficiencia.Should().BeEmpty();
        codigos.TiposDeDocumento.Should().BeEmpty();
        codigos.BaseLegalDoFada.Should().BeNull();
        codigos.Regras.Should().BeEmpty();
    }

    [Fact(DisplayName = "A oferta, a referência demográfica e a modalidade repetidas entre as distribuições vão uma vez, na ordem em que aparecem")]
    public void Distribuicoes_CitamCadaCodigoUmaVez()
    {
        VagasNoDocumento vagas = new(null, [
            Distribuicao(0, "OFERTA_B", "CENSO_2022", modalidades: ["AMPLA", "COTA_A", null]),
            Distribuicao(1, "OFERTA_A", "CENSO_2022", modalidades: ["COTA_A", "COTA_B"]),
            Distribuicao(2, "OFERTA_B", null),
            Distribuicao(3, null),
        ]);

        CodigosDasVagasEDasExigenciasCitados codigos = Codigos(vagas);

        codigos.OfertasDeCurso.Should().Equal("OFERTA_B", "OFERTA_A");
        codigos.ReferenciasDemograficas.Should().Equal("CENSO_2022");
        codigos.Modalidades.Should().Equal("AMPLA", "COTA_A", "COTA_B");
    }

    [Fact(DisplayName = "O ajuste do quadro e a cascata também citam modalidades, depois das da distribuição")]
    public void AjusteECascata_CitamModalidades()
    {
        VagasNoDocumento vagas = new(null, [Distribuicao(0, "OFERTA", modalidades: ["AMPLA"]) with { AjusteDoQuadro = new("reduzirProporcionalEm", null, ["COTA_A", "AMPLA"]) }])
        {
            Cascata = new(null, "FINAL", [new(0, "COTA_B", "COTA_C"), new(1, null, "COTA_A")]),
        };

        Codigos(vagas).Modalidades.Should().Equal("AMPLA", "COTA_A", "FINAL", "COTA_B", "COTA_C");
    }

    [Fact(DisplayName = "As regras da distribuição, do ajuste, da cascata e do FADA entram sem repetição, e a citação incompleta fica de fora")]
    public void Regras_EntramSemRepeticaoEOIncompletoFicaDeFora()
    {
        ReferenciaDeRegraNoDocumento distribuicao = new("DISTRIBUICAO", "v1");
        VagasNoDocumento vagas = new(null, [
            Distribuicao(0, "OFERTA_A", regra: distribuicao, ajuste: new("AJUSTE", "v1")),
            Distribuicao(1, "OFERTA_B", regra: distribuicao, ajuste: new("AJUSTE", null)),
        ])
        {
            Cascata = new(new("CASCATA", "v1"), null, []),
            Fada = new(true, new("FADA", "v2"), null, null, null, null),
        };

        Codigos(vagas).Regras.Should().Equal(("DISTRIBUICAO", "v1"), ("AJUSTE", "v1"), ("CASCATA", "v1"), ("FADA", "v2"));
    }

    [Fact(DisplayName = "O atendimento cita cada condição, recurso e tipo de deficiência uma vez, e o FADA cita a base legal")]
    public void AtendimentoEFada_CitamSeusCodigos()
    {
        VagasNoDocumento vagas = new(null, [])
        {
            Atendimento = new(
                [new(0, "PCD"), new(1, null), new(2, "PCD"), new(3, "TEA")],
                [new(0, "LIBRAS")],
                [new(0, "VISUAL"), new(1, "VISUAL")]),
            Fada = new(true, null, null, null, null, "PORTARIA_2514"),
        };

        CodigosDasVagasEDasExigenciasCitados codigos = Codigos(vagas);

        codigos.CondicoesDeAtendimento.Should().Equal("PCD", "TEA");
        codigos.RecursosDeAcessibilidade.Should().Equal("LIBRAS");
        codigos.TiposDeDeficiencia.Should().Equal("VISUAL");
        codigos.BaseLegalDoFada.Should().Be("PORTARIA_2514");
    }

    [Fact(DisplayName = "As exigências citam cada tipo de documento uma vez, na ordem em que aparecem")]
    public void Exigencias_CitamCadaTipoDeDocumentoUmaVez()
    {
        ExigenciasNoDocumento exigencias = new(
            [new(0, "a", "RG", null, null, null), new(1, "b", "CPF", null, null, null), new(2, "c", "RG", null, null, null), new(3, "d", null, null, null, null)],
            []);

        Codigos(null, exigencias).TiposDeDocumento.Should().Equal("RG", "CPF");
    }

    [Fact(DisplayName = "O que as vagas e as exigências citam se une ao que o resto do documento cita, sem repetir a regra")]
    public void CodigosDoDocumento_UnemComOResto()
    {
        DeclaracoesDoDocumento declaracoes = new(Identificacao, [])
        {
            Vagas = new(null, [Distribuicao(0, "OFERTA", regra: new("REGRA", "v1"), modalidades: ["AMPLA"])]) { Fada = new(true, new("REGRA", "v1"), null, null, null, "BASE") },
            Exigencias = new([new(0, "a", "RG", null, null, null)], []),
        };

        CodigosCitadosPorCadastro codigos = CodigosCitadosNoDocumento.Em(declaracoes);

        codigos.OfertasDeCurso.Should().Equal("OFERTA");
        codigos.Modalidades.Should().Equal("AMPLA");
        codigos.TiposDeDocumento.Should().Equal("RG");
        codigos.BaseLegalDoFada.Should().Be("BASE");
        codigos.Regras.Should().Equal(("REGRA", "v1"));
    }
}
