namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class RotulosGravadosPeloSerializadorNaExportacaoTests
{
    private static readonly EsquemaDoDocumentoDaConfiguracao Esquema = new();

    public static TheoryData<EfeitoSuspensivo> OpcoesDeEfeitoSuspensivo => [.. Enum.GetValues<EfeitoSuspensivo>()];

    [Fact]
    public void Exportar_ExigenciaGeral_LevaORotuloDaAplicabilidadeEOsDosObjetosAninhados()
    {
        DocumentoExigido exigencia = ExigenciaComPrazoEBaseLegal();

        JsonObject documento = Exportar(CongeladoComExigencia(exigencia));

        JsonObject exportada = documento["documentosExigidos"]!["exigencias"]![0]!.AsObject();
        exportada["aplicabilidade"]!.GetValue<string>().Should().Be("GERAL");
        exportada["aplicabilidadeRotulo"]!.GetValue<string>().Should().Be(Aplicabilidade.Geral.Rotulo());
        exportada["basesLegais"]![0]!["abrangenciaRotulo"]!.GetValue<string>().Should().Be(TipoAbrangencia.InternaNorma.Rotulo());
        exportada["prazoDeEmissao"]!["unidadeRotulo"]!.GetValue<string>().Should().Be(UnidadeDoPrazoDeEmissao.Dias.Rotulo());
        EstaValidoNoEsquema(documento);
    }

    [Fact]
    public void Exportar_RotuloMudadoNoCatalogoDepoisDaPublicacao_MantemORotuloDaEpoca()
    {
        JsonObject congelado = CongeladoComExigencia(ExigenciaComPrazoEBaseLegal());
        congelado["documentosExigidos"]!["exigencias"]![0]!["contexto"]!["aplicabilidadeRotulo"] = "Texto da época da publicação";

        JsonObject documento = Exportar(congelado);

        documento["documentosExigidos"]!["exigencias"]![0]!["aplicabilidadeRotulo"]!.GetValue<string>()
            .Should().Be("Texto da época da publicação").And.NotBe(Aplicabilidade.Geral.Rotulo());
    }

    [Fact]
    public void Exportar_TiposDeDecisaoDaFase_LevaOsRotulosNaMesmaOrdemDosCodigos()
    {
        JsonObject fase = new() { ["id"] = Guid.CreateVersion7(), ["chave"] = "fase", ["codigo"] = "INSCRICAO" };
        TipoMotivoDeDecisao[] tipos = [TipoMotivoDeDecisao.IsencaoDaTaxa, TipoMotivoDeDecisao.HomologacaoDasInscricoes];
        fase["tiposDeDecisao"] = TiposDeDecisaoNoEnvelope.Serializar(tipos);
        TiposDeDecisaoNoEnvelope.AcrescentarContexto(fase, "tiposDeDecisao", tipos);

        JsonObject documento = Exportar(new() { ["cronogramaFases"] = new JsonObject { ["fases"] = new JsonArray(fase) } });

        JsonObject exportada = documento["cronograma"]!["fases"]![0]!.AsObject();
        exportada["tiposDeDecisaoRotulos"]!.AsArray().Select(static rotulo => rotulo!.GetValue<string>())
            .Should().Equal(tipos.OrderBy(static tipo => tipo.Codigo(), StringComparer.Ordinal).Select(static tipo => tipo.Rotulo()));
        exportada["tiposDeDecisaoRotulos"]!.AsArray().Should().HaveSameCount(exportada["tiposDeDecisao"]!.AsArray());
        EstaValidoNoEsquema(documento);
    }

    [Theory]
    [MemberData(nameof(OpcoesDeEfeitoSuspensivo))]
    public void Exportar_RecursoDaFase_LevaOsRotulosDoRecurso(EfeitoSuspensivo efeito)
    {
        Guid publicacao = Guid.CreateVersion7();
        JsonObject recurso = SerializacaoDoCronograma.SerializarRegraRecursoFase(RegraDaFase(efeito, publicacao));
        JsonObject fase = new() { ["id"] = Guid.CreateVersion7(), ["chave"] = "fase", ["codigo"] = "INSCRICAO", ["regraRecurso"] = recurso, ["publicacoesPrevistas"] = Publicacoes(publicacao) };

        JsonObject documento = Exportar(new() { ["cronogramaFases"] = new JsonObject { ["fases"] = new JsonArray(fase) }, ["tiposDeAto"] = TiposDeAto() });

        ConferirRotulosDoRecurso(documento["cronograma"]!["fases"]![0]!["regraRecurso"]!.AsObject(), efeito);
        EstaValidoNoEsquema(documento);
    }

    [Theory]
    [MemberData(nameof(OpcoesDeEfeitoSuspensivo))]
    public void Exportar_RecursoDaEtapa_LevaOsRotulosDoRecursoEODoEventoInicial(EfeitoSuspensivo efeito)
    {
        JsonObject recurso = SerializacaoDasEtapas.SerializarRecurso(RecursoDaEtapaCom(efeito));
        JsonObject etapa = new() { ["chave"] = "etapa", ["recursos"] = new JsonArray(recurso) };

        JsonObject documento = Exportar(new() { ["etapas"] = new JsonArray(etapa) });

        JsonObject exportado = documento["etapas"]![0]!["recursos"]![0]!.AsObject();
        ConferirRotulosDoRecurso(exportado, efeito);
        exportado["eventoInicialDoPrazoRotulo"]!.GetValue<string>().Should().Be(EventoInicialDoPrazo.PublicacaoDoAto.Rotulo());
        EstaValidoNoEsquema(documento);
    }

    [Fact]
    public void Exportar_RecursoSemInstanciaSuperior_NaoTrazAUnidadeDaSegundaInstancia()
    {
        JsonObject parametros = SerializacaoDoCronograma.SerializarParametrosRegraPrazoRecurso(ParametrosDoRecursoDeTeste.Conferidos());
        JsonObject etapa = new() { ["chave"] = "etapa", ["recursos"] = new JsonArray(new JsonObject { ["eventoInicialDoPrazo"] = "PUBLICACAO_DO_ATO", ["parametros"] = parametros }) };

        JsonObject documento = Exportar(new() { ["etapas"] = new JsonArray(etapa) });

        JsonObject exportado = documento["etapas"]![0]!["recursos"]![0]!.AsObject();
        exportado.ContainsKey("prazoSegundaInstanciaUnidade").Should().BeFalse();
        exportado.ContainsKey("prazoSegundaInstanciaUnidadeRotulo").Should().BeFalse();
    }

    private static void ConferirRotulosDoRecurso(JsonObject recurso, EfeitoSuspensivo efeito)
    {
        recurso["prazoUnidadeRotulo"]!.GetValue<string>().Should().Be(UnidadePrazo.DiasUteis.Rotulo());
        recurso["prazoSegundaInstanciaUnidadeRotulo"]!.GetValue<string>().Should().Be(UnidadePrazo.Horas.Rotulo());
        recurso["efeitoSuspensivoRotulo"]!.GetValue<string>().Should().Be(efeito.Rotulo());
        recurso["guardaDaVagaRotulo"]!.GetValue<string>().Should().Be(GuardaDaVaga.AteOResultadoDefinitivo.Rotulo());
    }

    private static JsonObject Exportar(JsonObject congelado) =>
        new ProjecaoDaVersaoPublicada(Esquema).Projetar(new(Guid.Empty, 1, DateTimeOffset.UnixEpoch, congelado.ToJsonString()));

    private static void EstaValidoNoEsquema(JsonObject documento)
    {
        IReadOnlyList<FieldError> erros = Esquema.Avaliar(documento);
        erros.Should().BeEmpty("as violações do esquema são: {0}", string.Join("; ", erros.Select(static erro => $"{erro.Field}: {erro.Error.Message}")));
    }

    private static JsonObject CongeladoComExigencia(DocumentoExigido exigencia) => new()
    {
        ["cronogramaFases"] = new JsonObject { ["fases"] = new JsonArray(new JsonObject { ["id"] = exigencia.ExigidoNaFaseId, ["chave"] = "fase", ["codigo"] = "INSCRICAO" }) },
        ["documentosExigidos"] = new JsonObject { ["exigencias"] = SerializacaoDasExigencias.SerializarExigencias([exigencia]) },
    };

    private static DocumentoExigido ExigenciaComPrazoEBaseLegal() => DocumentoExigidoDeTeste.Novo()
        .ComPrazoDeEmissao(new PrazoDeEmissaoBuilder().Construir())
        .ComBasesLegais([DocumentoExigidoBaseLegal.Criar("Resolução fictícia 1/2027, art. 1º", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, null).Value!])
        .Criar().Value!;

    private static ReferenciaRegra Regra() =>
        ReferenciaRegra.De(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), "v1", new string('a', 64));

    private static ParametrosDeclaradosDoRecurso ComInstanciaSuperior(EfeitoSuspensivo efeito) =>
        ParametrosDoRecursoDeTeste.Declarados(
            2m, UnidadePrazo.DiasUteis, admiteInstanciaSuperior: true, segundaInstancia: new PrazoDoRecurso(3m, UnidadePrazo.Horas), efeitoSuspensivo: efeito.Codigo());

    private static RegraRecursoFase RegraDaFase(EfeitoSuspensivo efeito, Guid publicacao) =>
        RegraRecursoFase.Criar(Regra(), ComInstanciaSuperior(efeito), publicacao).Value!;

    private static RecursoDaEtapa RecursoDaEtapaCom(EfeitoSuspensivo efeito) =>
        RecursoDaEtapa.Criar(EventoInicialDoPrazo.PublicacaoDoAto, Regra(), ComInstanciaSuperior(efeito), Guid.Empty).Value!;

    private static JsonArray Publicacoes(Guid publicacao) =>
        new(new JsonObject { ["id"] = publicacao, ["atoCodigo"] = "RESULTADO", ["papel"] = "PRELIMINAR" });

    private static JsonArray TiposDeAto() =>
        new(new JsonObject { ["codigo"] = "RESULTADO", ["nome"] = "Resultado", ["unicoPorObjeto"] = false, ["ehResultado"] = true, ["congelaConfiguracao"] = false });
}
