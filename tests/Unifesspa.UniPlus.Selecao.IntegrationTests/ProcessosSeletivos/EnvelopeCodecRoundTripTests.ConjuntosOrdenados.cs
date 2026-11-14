namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

public sealed partial class EnvelopeCodecRoundTripTests
{
    /// <summary>
    /// Prova que o decoder NÃO reordena (ADR-0109 D9) com os seis arrays da issue #1067
    /// povoados ao mesmo tempo: <c>ocorrenciasEsperadas</c>,
    /// <c>documentosExigidos.exigencias[].formatosPermitidos.lista</c>,
    /// <c>documentosExigidos.obrigatoriedades</c>, os dois <c>predicado.args</c>
    /// (<c>codigos</c>/<c>necessidades</c>) e <c>documentosExigidos.metadadosFatos[].valoresDominio</c>.
    /// </summary>
    /// <remarks>
    /// A sequência esperada aqui NÃO é um oráculo escrito à mão — essa prova já existe à parte,
    /// por array, no canonicalizador puro. O que só este teste prova é o risco descrito em D2: um
    /// decoder que lesse um destes arrays num dicionário ou conjunto e devolvesse os valores JÁ
    /// reordenados pela MESMA chave que o encoder usa produziria bytes idênticos na
    /// recodificação — passaria pela asserção de bytes sozinha. Por isso a sequência esperada é
    /// extraída do PRÓPRIO JSON congelado e comparada contra o valor <b>imediatamente</b>
    /// decodificado, antes de qualquer recodificação: só um decoder que preserva a ordem lida
    /// (um laço sequencial para <c>List&lt;T&gt;</c>, nunca um dicionário) bate as duas.
    /// </remarks>
    [Fact(DisplayName = "Round-trip com os seis conjuntos da issue #1067 povoados — bytes idênticos e a sequência imediatamente decodificada bate com o JSON")]
    public void RoundTrip_SeisConjuntosDaIssue1067Povoados_PreservaSequenciaImediatamenteDecodificada()
    {
        (ProcessoSeletivo processo, FaseCronograma fase) = ProcessoDeInscricao("PS Seis Conjuntos");
        DefinirFolhaComOcorrenciasEFormatos(processo, fase);
        EntradaCanonicalizacao entrada = EntradaDosSeisConjuntos(processo);
        SnapshotCanonico congelado = new SnapshotPublicacaoCanonicalizer().Canonicalizar(entrada);
        ConferirSchemaCorrente(congelado);
        VersaoConfiguracao v1 = Publicado(processo, entrada, congelado, "user-sub-seis-conjuntos");
        SequenciasNoJson noJson = SequenciasDoJson(Envelope(congelado));
        EnvelopeReidratado envelope = Reidratado(v1);
        ConferirSequenciasDecodificadas(envelope, noJson);
        processo.RestaurarConfiguracaoCongelada(v1, envelope.Grafo).IsSuccess.Should().BeTrue();
        ConferirQueARecodificacaoReproduzOsBytes(
            RecodificadoSemInsumosDeRecomputacao(processo, v1, envelope), congelado, "os seis conjuntos da issue #1067, todos povoados ao mesmo tempo");
    }

    private static EntradaCanonicalizacao EntradaDosSeisConjuntos(ProcessoSeletivo processo) =>
        new(
            processo, DadosDeEditalDoEnvelope(), "3333333333333333333333333333333333333333333333333333333333333333", FusoInstitucional.ZoneId,
            Conformidade: ConformidadeComArgumentosEmOrdemNaoAlfabetica(), MetadadosFatosCongelados: MetadadosComValoresEmOrdemNaoAlfabetica(),
            ValoresSelecionaveisCongelados: CatalogoDoConjuntoBasico.ComValoresCongelados(),
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao);

    private sealed record SequenciasNoJson(
        IReadOnlyList<string> Ocorrencias,
        IReadOnlyList<string> Formatos,
        IReadOnlyList<string> RegraCodigos,
        IReadOnlyList<string> ValoresDominio,
        IReadOnlyList<string> Codigos,
        IReadOnlyList<string> Necessidades);

    private static void DefinirFolhaComOcorrenciasEFormatos(ProcessoSeletivo processo, FaseCronograma fase)
    {
        NoExigencia folha = NoExigencia.CriarFolha(
            DocumentoDeIdentidadeComFormatosEnumerados(fase), 0, quantidadeMinima: 2, chaveDistincao: ChaveDistincao.Ocorrencia,
            ocorrenciasEsperadas: ["ocorrencia_zeta", "ocorrencia_alfa"]).Value!;
        processo.DefinirDocumentosExigidos([folha], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
    }

    private static DocumentoExigido DocumentoDeIdentidadeComFormatosEnumerados(FaseCronograma fase) =>
        DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "RG", "Documento de identidade", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            [], [], null,
            FormatosPermitidos.Criar(qualquer: false, entradas: [("PNG", null), ("JPEG", null), ("PDF", null)]).Value!,
            null, finalidade: FinalidadeFormulario.Inscricao).Value!;

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
            Ocorrencias: Textos(envelope["arvoreSatisfacao"]![0]!["ocorrenciasEsperadas"]!.AsArray(), static o => o),
            Formatos: Textos(envelope["documentosExigidos"]!["exigencias"]![0]!["formatosPermitidos"]!["lista"]!.AsArray(), static f => f["formato"]),
            RegraCodigos: Textos(obrigatoriedades, static r => r["regraCodigo"]),
            ValoresDominio: Textos(envelope["documentosExigidos"]!["metadadosFatos"]![0]!["valoresDominio"]!.AsArray(), static v => v),
            Codigos: Textos(ArgumentosDoPredicado(obrigatoriedades, "REGRA-MODALIDADES-MINIMAS")["codigos"]!.AsArray(), static c => c),
            Necessidades: Textos(ArgumentosDoPredicado(obrigatoriedades, "REGRA-ATENDIMENTO-DISPONIVEL")["necessidades"]!.AsArray(), static n => n));
    }

    private static IReadOnlyList<string> Textos(JsonArray itens, Func<JsonNode, JsonNode?> selecao) =>
        [.. itens.Select(item => selecao(item!)!.GetValue<string>())];

    private static JsonNode ArgumentosDoPredicado(JsonArray obrigatoriedades, string regraCodigo) =>
        obrigatoriedades
            .Single(r => r!["regraCodigo"]!.GetValue<string>() == regraCodigo)!
            ["predicado"]!["args"]!;

    /// <summary>A sequência IMEDIATAMENTE decodificada — antes de qualquer recodificação.</summary>
    private static void ConferirSequenciasDecodificadas(EnvelopeReidratado envelope, SequenciasNoJson noJson)
    {
        envelope.Grafo.NosExigencia.Single().OcorrenciasEsperadas.Should().Equal(
            noJson.Ocorrencias, "o decoder tem de preservar a sequência que leu — nunca recalculá-la");
        envelope.Grafo.DocumentosExigidos.Single().FormatosPermitidos.Lista!
            .Select(static f => f.Formato.ToCodigo()).Should().Equal(noJson.Formatos);
        envelope.Conformidade!.Regras.Select(static r => r.RegraCodigo).Should().Equal(noJson.RegraCodigos);
        envelope.MetadadosFatosCongelados!["COR_RACA"].ValoresDominio.Should().Equal(noJson.ValoresDominio);
        ((ModalidadesMinimas)PredicadoDaRegra(envelope, "REGRA-MODALIDADES-MINIMAS")).Codigos.Should().Equal(noJson.Codigos);
        ((AtendimentoDisponivel)PredicadoDaRegra(envelope, "REGRA-ATENDIMENTO-DISPONIVEL")).Necessidades.Should().Equal(noJson.Necessidades);
    }

    private static PredicadoObrigatoriedade PredicadoDaRegra(EnvelopeReidratado envelope, string regraCodigo) =>
        envelope.Conformidade!.Regras.Single(r => r.RegraCodigo == regraCodigo).Predicado;

    private static byte[] RecodificadoSemInsumosDeRecomputacao(
        ProcessoSeletivo processo, VersaoConfiguracao versao, EnvelopeReidratado envelope) =>
        CorpusEnvelope.Registro.Recodificar(
            versao.SchemaVersion,
            new EntradaCanonicalizacao(
                processo, envelope.Dados, envelope.HashDocumento, FusoInstitucional.ZoneId, envelope.Retificacao, envelope.Conformidade,
                envelope.MetadadosFatosCongelados, envelope.ValoresSelecionaveisCongelados,
                TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao)).Value!.Bytes;

    internal static JsonObject Envelope(SnapshotCanonico snapshot) =>
        JsonNode.Parse(Encoding.UTF8.GetString(snapshot.Bytes))!.AsObject();

    /// <summary>
    /// Troca <b>um</b> valor num path <c>a.0.b</c>, preservando o <b>tipo JSON</b> do
    /// original — o envelope escreve decimais como string e inteiros como número, e trocar
    /// um pelo outro testaria o parser de tipos, não a leitura do campo.
    /// </summary>
    private static void Mutar(JsonObject raiz, string caminho, string valorNovo)
    {
        string[] partes = caminho.Split('.');
        JsonNode atual = raiz;

        for (int i = 0; i < partes.Length - 1; i++)
        {
            atual = int.TryParse(partes[i], CultureInfo.InvariantCulture, out int indice)
                ? atual.AsArray()[indice]!
                : atual.AsObject()[partes[i]]!;
        }

        string chave = partes[^1];

        if (int.TryParse(chave, CultureInfo.InvariantCulture, out int posicao))
        {
            JsonArray array = atual.AsArray();
            array[posicao].Should().NotBeNull($"pré-condição: o path '{caminho}' tem de existir no envelope");
            array[posicao] = ValorComoNo(array[posicao]!, valorNovo);
            return;
        }

        JsonObject objeto = atual.AsObject();
        objeto[chave].Should().NotBeNull($"pré-condição: o path '{caminho}' tem de existir no envelope");
        objeto[chave] = ValorComoNo(objeto[chave]!, valorNovo);
    }

    /// <summary>
    /// Sem o ramo <c>True</c>/<c>False</c>, os campos booleanos mutados por este helper
    /// (<c>baseadoEmEnem</c>, <c>notaDeOrigemNoEnem</c>) virariam string JSON e seriam
    /// recusados por <see cref="LeitorEnvelope.Booleano"/>, que só aceita
    /// <c>JsonValueKind.True</c>/<c>False</c>.
    /// </summary>
    private static JsonValue ValorComoNo(JsonNode original, string valor) => original.GetValueKind() switch
    {
        System.Text.Json.JsonValueKind.Number => JsonValue.Create(int.Parse(valor, CultureInfo.InvariantCulture)),
        System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False =>
            JsonValue.Create(bool.Parse(valor)),
        _ => JsonValue.Create(valor),
    };

    // ── Round-trip dedicado de baseadoEmEnem — corpus SEM eliminação ENEM (#850) ──
}
