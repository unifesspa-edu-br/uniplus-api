namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDoEnvelopeCodec;

public sealed class EnvelopeLidoConjuntosOrdenadosTests
{
    [Fact(DisplayName = "Round-trip com os seis conjuntos da issue #1067 povoados — bytes idênticos e a sequência imediatamente decodificada bate com o JSON")]
    public void RoundTrip_SeisConjuntosDaIssue1067Povoados_PreservaSequenciaImediatamenteDecodificada()
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ProcessoDeInscricao("PS Seis Conjuntos");
        DefinirRegraComOcorrenciasEFormatos(processo, fase);
        EntradaCanonicalizacao entrada = EntradaDosSeisConjuntos(processo);
        SnapshotCanonico congelado = new SnapshotPublicacaoCanonicalizer().Canonicalizar(entrada);
        ConferirSchemaCorrente(congelado);
        VersaoConfiguracao v1 = Publicado(processo, entrada, congelado, "user-conta-seis-conjuntos");
        SequenciasNoJson noJson = SequenciasDoJson(Envelope(congelado));
        ConferirQuantidadesDosConjuntos(noJson);
        EnvelopeReidratado envelope = FidelidadeDoEnvelopeDeTeste.Conferir(processo, v1, congelado);
        ConferirSequenciasDecodificadas(envelope, noJson);
    }

    private static EntradaCanonicalizacao EntradaDosSeisConjuntos(ProcessoSeletivo processo) =>
        new(
            processo, DadosDeEditalDoEnvelope(), "3333333333333333333333333333333333333333333333333333333333333333", FusoInstitucional.ZoneId,
            null, ConformidadeComArgumentosEmOrdemNaoAlfabetica(), new DadosDeCatalogoDaConfiguracaoPublicada(MetadadosFatosCongelados: MetadadosComValoresEmOrdemNaoAlfabetica(),
            ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(),
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao));

    private sealed record SequenciasNoJson(
        IReadOnlyList<string> Ocorrencias,
        IReadOnlyList<string> Formatos,
        IReadOnlyList<string> RegraCodigos,
        IReadOnlyList<string> ValoresDominio,
        IReadOnlyList<string> Codigos,
        IReadOnlyList<string> Necessidades);

    private static void DefinirRegraComOcorrenciasEFormatos(ProcessoSeletivo processo, FaseCronograma fase)
    {
        RegraDeComprovacao regraDeDocumento = RegraDeComprovacao.CriarRegraDeDocumento(
            DocumentoDeIdentidadeComFormatosEnumerados(fase), 0, quantidadeMinima: 2, criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.Ocorrencia,
            ocorrenciasEsperadas: ["ocorrencia_zeta", "ocorrencia_alfa"]).Value!;
        processo.DefinirDocumentosExigidos([regraDeDocumento]).IsSuccess.Should().BeTrue();
    }

    private static DocumentoExigido DocumentoDeIdentidadeComFormatosEnumerados(FaseCronograma fase) =>
        DocumentoExigido.Criar(
            Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("exigencia_rg").Value!,
            new DocumentoExigidoDeclarado(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "RG", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal),
                new MomentoDaExigencia(fase.Id, null, FinalidadeFormulario.Inscricao),
                new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []),
                new IndeferimentoDoDocumento(false, null, []),
                RequisitosDoDocumentoDeTeste.Com(FormatosPermitidos.Criar(qualquer: false, entradas: [("PNG", null), ("JPEG", null), ("PDF", null)]).Value!),
                null)).Value!;

    private static ResultadoConformidade ConformidadeComArgumentosEmOrdemNaoAlfabetica() =>
        new(
            [
                RegraAprovada("REGRA-MODALIDADES-MINIMAS", new ModalidadesMinimas(["QUI", "AC", "LB_PPI"]), "Regra de modalidades mínimas", 'm'),
                RegraAprovada("REGRA-ATENDIMENTO-DISPONIVEL", new AtendimentoDisponivel(["VISUAL", "AUDITIVA", "FISICA"]), "Regra de atendimento disponível", 'n'),
            ],
            []);

    private static RegraAvaliada RegraAprovada(string codigo, PredicadoObrigatoriedade predicado, string descricao, char hash) =>
        new(
            RegraId: Guid.CreateVersion7(),
            RegraCodigo: codigo,
            Categoria: CategoriaObrigatoriedade.Outros,
            TipoProcessoCodigoAvaliado: "SiSU",
            Predicado: predicado,
            Aprovada: true,
            Motivo: null,
            BaseLegal: "Lei de teste",
            AtoNormativoUrl: null,
            PortariaInterna: null,
            DescricaoHumana: descricao,
            VigenciaInicio: new DateOnly(2020, 1, 1),
            VigenciaFim: null,
            Hash: new string(hash, 64));

    private static Dictionary<string, MetadadoFatoCongelado> MetadadosComValoresEmOrdemNaoAlfabetica() =>
        new(StringComparer.Ordinal)
        {
            ["COR_RACA"] = new MetadadoFatoCongelado(
                Codigo: "COR_RACA",
                Dominio: "CATEGORICO",
                Origem: "DECLARADO",
                Cardinalidade: "ESCALAR",
                PontoResolucao: "INSCRICAO",
                Binding: "CAMPO_FORMULARIO:COR_RACA",
                ValoresDominio: ["ZETA_VALOR", "ALFA_VALOR"],
                ValoresDominioDeclarados: null),
        };

    private static SequenciasNoJson SequenciasDoJson(JsonObject envelope)
    {
        JsonArray obrigatoriedades = envelope["documentosExigidos"]!["obrigatoriedades"]!.AsArray();
        return new SequenciasNoJson(
            Ocorrencias: Textos(envelope["regrasDeComprovacao"]![0]!["ocorrenciasEsperadas"]!.AsArray(), static o => o),
            Formatos: Textos(envelope["documentosExigidos"]!["exigencias"]![0]!["formatosPermitidos"]!["lista"]!.AsArray(), static f => f["formato"]),
            RegraCodigos: Textos(obrigatoriedades, static r => r["regraCodigo"]),
            ValoresDominio: Textos(envelope["documentosExigidos"]!["definicoesDosDados"]![0]!["opcoes"]!.AsArray(), static v => v),
            Codigos: Textos(ArgumentosDoPredicado(obrigatoriedades, "REGRA-MODALIDADES-MINIMAS")["codigos"]!.AsArray(), static c => c),
            Necessidades: Textos(ArgumentosDoPredicado(obrigatoriedades, "REGRA-ATENDIMENTO-DISPONIVEL")["necessidades"]!.AsArray(), static n => n));
    }

    private static IReadOnlyList<string> Textos(JsonArray itens, Func<JsonNode, JsonNode?> selecao) =>
        [.. itens.Select(item => selecao(item!)!.GetValue<string>())];

    private static JsonNode ArgumentosDoPredicado(JsonArray obrigatoriedades, string regraCodigo) =>
        obrigatoriedades
            .Single(r => r!["regraCodigo"]!.GetValue<string>() == regraCodigo)!
            ["condicoes"]!["parametros"]!;
    private static void ConferirSequenciasDecodificadas(EnvelopeReidratado envelope, SequenciasNoJson noJson)
    {
        envelope.Configuracao.Exigencias.RegrasDeComprovacao.Single().Distincao!.OcorrenciasEsperadas.Should().Equal(
            noJson.Ocorrencias, "o decoder tem de preservar a sequência que leu — nunca recalculá-la");
        envelope.Configuracao.Exigencias.DocumentosExigidos.Single().FormatosPermitidos.Lista!
            .Select(static f => f.Formato.ToCodigo()).Should().Equal(noJson.Formatos);
        envelope.Conformidade!.Regras.Select(static r => r.RegraCodigo).Should().Equal(noJson.RegraCodigos);
        envelope.DadosDeCatalogo.MetadadosFatosCongelados!["COR_RACA"].ValoresDominio.Should().Equal(noJson.ValoresDominio);
        ((ModalidadesMinimas)PredicadoDaRegra(envelope, "REGRA-MODALIDADES-MINIMAS")).Codigos.Should().Equal(noJson.Codigos);
        ((AtendimentoDisponivel)PredicadoDaRegra(envelope, "REGRA-ATENDIMENTO-DISPONIVEL")).Necessidades.Should().Equal(noJson.Necessidades);
    }

    private static PredicadoObrigatoriedade PredicadoDaRegra(EnvelopeReidratado envelope, string regraCodigo) =>
        envelope.Conformidade!.Regras.Single(r => r.RegraCodigo == regraCodigo).Predicado;


    private static void ConferirQuantidadesDosConjuntos(SequenciasNoJson sequencias)
    {
        sequencias.Ocorrencias.Should().HaveCount(2);
        sequencias.Formatos.Should().HaveCount(3);
        sequencias.RegraCodigos.Should().HaveCount(2);
        sequencias.ValoresDominio.Should().HaveCount(2);
        sequencias.Codigos.Should().HaveCount(3);
        sequencias.Necessidades.Should().HaveCount(3);
    }
}
