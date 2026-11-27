namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os códigos que as etapas e a classificação citam saem sem repetição e na ordem em que aparecem; a regra citada sem código ou sem versão não consulta o catálogo.</summary>
public sealed class CodigosCitadosNaAvaliacaoTests
{
    private static readonly ParametrosDeclaradosDoRecurso Prazo = new(new PrazoDeclarado(1m, "HORAS"), false, PrazoDeclarado.Ausente, "SEM_EFEITO_SUSPENSIVO", null);

    private static readonly IdentificacaoNoDocumento Identificacao = new(null, null, null, null, null, null, null);

    private static ReferenciaDeRegraNoDocumento Regra(string codigo, string versao) => new(codigo, versao);

    private static EtapaNoDocumento Etapa(int posicao, string? tipo = null, ReferenciaDeRegraNoDocumento? apuracao = null) => new(
        posicao,
        $"etapa_{posicao}",
        new(null, tipo, null, null, null, null),
        new(apuracao, null, null, null, [], []),
        new(null, null),
        []);

    private static EtapaNoDocumento ComBancas(EtapaNoDocumento etapa, params string[] tipos) =>
        etapa with { Bancas = [.. tipos.Select(static (tipo, posicao) => new BancaNoDocumento(posicao, $"banca_{posicao}", tipo, [], null))] };

    private static EtapaNoDocumento ComAtos(EtapaNoDocumento etapa, params string[] atos) =>
        etapa with { PublicacoesPrevistas = [.. atos.Select(static (ato, posicao) => new ProdutoNoDocumento(posicao, ato, null))] };

    private static EtapaNoDocumento ComRecurso(EtapaNoDocumento etapa, ReferenciaDeRegraNoDocumento regra, params ReferenciaDeRegraNoDocumento[] procedimentos) =>
        etapa with
        {
            Recursos = [new(0, null, new(regra, "ATO_DE_REFERENCIA", Prazo,
                [.. procedimentos.Select(static (procedimento, posicao) => new ProcedimentoNoDocumento(posicao, TipoMotivoDeDecisao.NotaDaEtapa, null, procedimento))]))],
        };

    private static CodigosDaAvaliacaoCitados Codigos(ClassificacaoNoDocumento? classificacao, params EtapaNoDocumento[] etapas) =>
        CodigosCitadosNaAvaliacao.Em(new DeclaracoesDoDocumento(Identificacao, []) { Etapas = etapas, Classificacao = classificacao });

    private static ClassificacaoNoDocumento Classificacao(string? resolucao = null) =>
        new(null, null, null, null, null, null, resolucao);

    [Fact(DisplayName = "O documento sem etapa nem classificação não cita código algum")]
    public void SemEtapaNemClassificacao_NaoCitaCodigo()
    {
        CodigosDaAvaliacaoCitados codigos = Codigos(null);

        new[] { codigos.TiposDeEtapa, codigos.TiposDeBanca, codigos.TiposDeAto }.Should().AllSatisfy(static lista => lista.Should().BeEmpty());
        codigos.Regras.Should().BeEmpty();
        codigos.ResolucaoDePesos.Should().BeNull();
    }

    [Fact(DisplayName = "O tipo de etapa citado por várias etapas vale uma vez, na ordem em que aparece, e a etapa sem tipo não cita")]
    public void TipoDeEtapaRepetido_ValeUmaVez()
    {
        CodigosDaAvaliacaoCitados codigos = Codigos(null, Etapa(0, "REDACAO"), Etapa(1), Etapa(2, "ENTREVISTA"), Etapa(3, "REDACAO"));

        codigos.TiposDeEtapa.Should().Equal("REDACAO", "ENTREVISTA");
    }

    [Fact(DisplayName = "O tipo de banca e o ato publicado pelas etapas valem uma vez cada, mesmo citados em etapas diferentes")]
    public void TipoDeBancaEAto_ValemUmaVez()
    {
        EtapaNoDocumento primeira = ComAtos(ComBancas(Etapa(0), "BANCA_DOCUMENTAL", "BANCA_COLEGIADA"), "AVISO", "RESULTADO_PRELIMINAR");
        EtapaNoDocumento segunda = ComAtos(ComBancas(Etapa(1), "BANCA_COLEGIADA"), "RESULTADO_PRELIMINAR", "RESULTADO_DEFINITIVO");

        CodigosDaAvaliacaoCitados codigos = Codigos(null, primeira, segunda);

        codigos.TiposDeBanca.Should().Equal("BANCA_DOCUMENTAL", "BANCA_COLEGIADA");
        codigos.TiposDeAto.Should().Equal("AVISO", "RESULTADO_PRELIMINAR", "RESULTADO_DEFINITIVO");
    }

    [Fact(DisplayName = "O ato de referência do recurso não é consultado: ele é um dos atos que a própria etapa publica")]
    public void AtoDeReferenciaDoRecurso_NaoEConsultado()
    {
        CodigosDaAvaliacaoCitados codigos = Codigos(null, ComRecurso(Etapa(0), Regra("PRAZO", "v1")));

        codigos.TiposDeAto.Should().BeEmpty();
    }

    [Fact(DisplayName = "A regra vem da apuração, do recurso e dos procedimentos das etapas, cada par uma vez")]
    public void Regras_VemDaApuracaoDoRecursoEDosProcedimentos()
    {
        EtapaNoDocumento primeira = ComRecurso(Etapa(0, apuracao: Regra("APURACAO", "v1")), Regra("PRAZO", "v1"), Regra("CONFERENCIA", "v1"), Regra("CONFERENCIA", "v1"));
        EtapaNoDocumento segunda = ComRecurso(Etapa(1, apuracao: Regra("APURACAO", "v1")), Regra("PRAZO", "v1"), Regra("CONFERENCIA", "v2"));

        CodigosDaAvaliacaoCitados codigos = Codigos(null, primeira, segunda);

        codigos.Regras.Should().Equal(("APURACAO", "v1"), ("PRAZO", "v1"), ("CONFERENCIA", "v1"), ("CONFERENCIA", "v2"));
    }

    [Fact(DisplayName = "A regra vem também do cálculo, do arredondamento, da ordem de alocação, das eliminações e dos desempates")]
    public void Regras_VemDaClassificacao()
    {
        ClassificacaoNoDocumento classificacao = Classificacao() with
        {
            RegraCalculo = new("CALCULO", "v1"),
            RegraArredondamento = new("ARREDONDAMENTO", "v1"),
            RegraOrdemAlocacao = new("ORDEM", "v1"),
            RegrasEliminacao = [new(0, new("ELIMINACAO_A", "v1"), null, null, null, null), new(1, new("ELIMINACAO_A", "v1"), null, null, null, null)],
            CriteriosDeDesempate = [new(0, new("DESEMPATE_A", "v1"), new(null, null, [])), new(1, new("DESEMPATE_B", "v2"), new(null, null, []))],
        };

        CodigosDaAvaliacaoCitados codigos = Codigos(classificacao);

        codigos.Regras.Should().Equal(
            ("CALCULO", "v1"), ("ARREDONDAMENTO", "v1"), ("ORDEM", "v1"), ("ELIMINACAO_A", "v1"), ("DESEMPATE_A", "v1"), ("DESEMPATE_B", "v2"));
    }

    [Fact(DisplayName = "A regra citada sem código ou sem versão não consulta o catálogo")]
    public void RegraSemCodigoOuSemVersao_NaoConsultaOCatalogo()
    {
        ClassificacaoNoDocumento classificacao = Classificacao() with
        {
            RegraCalculo = new("CALCULO", null),
            RegraArredondamento = new(null, "v1"),
            CriteriosDeDesempate = [new(0, new(null, null), new(null, null, []))],
        };

        CodigosDaAvaliacaoCitados codigos = Codigos(classificacao, Etapa(0, apuracao: new("APURACAO", null)));

        codigos.Regras.Should().BeEmpty();
    }

    [Fact(DisplayName = "A resolução dos pesos é a que a classificação cita, e a classificação sem resolução não cita nenhuma")]
    public void ResolucaoDePesos_EADaClassificacao()
    {
        Codigos(Classificacao("Res. 805/2024")).ResolucaoDePesos.Should().Be("Res. 805/2024");
        Codigos(Classificacao()).ResolucaoDePesos.Should().BeNull();
    }

    [Fact(DisplayName = "A união dos cadastros soma o que as etapas citam ao que as fases citam, sem repetir código")]
    public void Uniao_SomaAvaliacaoEFasesSemRepetir()
    {
        FaseNoDocumento fase = new(0, "fase_0", new("ANALISE", null), new(null, null), new([new(0, "AVISO", null)], false, null, null),
            [new(0, "banca_0", "BANCA_DOCUMENTAL", ["RENDA"], null)], null);
        EtapaNoDocumento etapa = ComAtos(ComBancas(Etapa(0, "REDACAO"), "BANCA_DOCUMENTAL", "BANCA_COLEGIADA"), "AVISO", "RESULTADO");
        ClassificacaoNoDocumento classificacao = Classificacao("Res. 805/2024") with { RegraCalculo = new("CALCULO", "v1") };

        CodigosCitadosPorCadastro codigos = CodigosCitadosNoDocumento.Em(
            new DeclaracoesDoDocumento(Identificacao with { AlgoritmoContagemPrazo = new("CALCULO", "v1") }, [fase]) { Etapas = [etapa], Classificacao = classificacao });

        codigos.TiposDeBanca.Should().Equal("BANCA_DOCUMENTAL", "BANCA_COLEGIADA");
        codigos.TiposDeAto.Should().Equal("AVISO", "RESULTADO");
        codigos.Regras.Should().Equal(("CALCULO", "v1"));
        codigos.TiposDeEtapa.Should().Equal("REDACAO");
        codigos.ResolucaoDePesos.Should().Be("Res. 805/2024");
    }
}
