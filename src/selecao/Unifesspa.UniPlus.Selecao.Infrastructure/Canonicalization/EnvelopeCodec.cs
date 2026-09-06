namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O codec do envelope de congelamento — <b>um só</b>. Enquanto não há produção nem certame
/// congelado, o sistema emite e lê uma forma canônica corrente (ver <see cref="SchemaVersion"/>)
/// e a evolui livremente: mudar a forma reescreve a fixture, não gera um encoder congelado ao
/// lado. O
/// versionamento forense — um codec por <c>schema_version</c>, encoders aposentados só quando
/// deixam de ser correntes — volta a valer no primeiro certame publicado em qualquer ambiente,
/// inclusive homologação. Não se espera a primeira release de produção: publicar em homologação
/// já cria um envelope que precisa ser preservado.
/// </summary>
/// <remarks>
/// <c>Codificar</c> delega ao <see cref="SnapshotPublicacaoCanonicalizer"/>, a projeção viva —
/// e, sendo o único codec, os dois nunca podem divergir. <c>Decodificar</c> reconstrói o
/// envelope pelos leitores de bloco (os métodos <c>Ler*</c> estáticos, divididos por assunto
/// em arquivos parciais desta classe) e resolve <c>arvoreSatisfacao</c> contra as exigências
/// já lidas, por Id, sem duplicá-las.
/// </remarks>
public sealed partial class EnvelopeCodec : IEnvelopeCodec
{
    private static readonly string[] BlocosReais =
    [
        "tipoProcesso",
        "periodo",
        "etapas",
        "distribuicao",
        "modalidades",
        "ofertas",
        "atendimento",
        "bonusRegional",
        "cascataRemanejamento",
        "criteriosDesempate",
        "classificacao",
        "hashesEdital",
        "cronogramaFases",
        "documentosExigidos",
        "vagas",
        "arvoreSatisfacao",
        "formulario",
        "divulgacao",
        "identidadesUnidade",
        "fatosColetados",
        "regrasDerivacao",
        "grafoDependencia",
        "versaoInterpretador",
        "modalidadesOfertadas",
        "taxaInscricao",
        "localidade",
        "algoritmoContagemPrazo",
        "calendarioDiasUteis",
    ];

    /// <summary>
    /// Chave duplicada é <b>erro</b>, não “a última ganha”. Um envelope com
    /// <c>"fator"</c> duas vezes tem duas leituras possíveis, e a que o hash cobre é
    /// indistinguível da que o parser escolheria.
    /// </summary>
    private static readonly JsonDocumentOptions OpcoesDocumento = new() { AllowDuplicateProperties = false };

    private const int EscalaPadrao = 4;

    private const int EscalaPercentual = 2;

    private readonly SnapshotPublicacaoCanonicalizer _encoder = new();

    public string SchemaVersion => "0.0.14";

    public IPerfilCanonico Perfil => PerfilCanonicoV1.Instancia;

    public string AlgoritmoHash => Perfil.Algoritmo;

    public bool TemEncoder => true;

    public bool TemDecoder => true;

    public string? MotivoDaRecusa => null;

    /// <summary>
    /// Delega à projeção viva. A guarda confere que o codec e o canonicalizador declaram a
    /// mesma versão e o mesmo algoritmo — sendo um sistema só, eles têm de concordar; uma
    /// divergência aqui é erro de programação (o codec e a emissão saíram de sincronia), não
    /// um estado alcançável em runtime.
    /// </summary>
    public SnapshotCanonico Codificar(EntradaCanonicalizacao entrada)
    {
        SnapshotCanonico snapshot = _encoder.Canonicalizar(entrada);

        if (snapshot.SchemaVersion != SchemaVersion || snapshot.AlgoritmoHash != AlgoritmoHash)
        {
            throw new InvalidOperationException(
                $"O codec ({SchemaVersion}/{AlgoritmoHash}) e o canonicalizador ({snapshot.SchemaVersion}/{snapshot.AlgoritmoHash}) " +
                "declaram versões distintas — o codec e a emissão saíram de sincronia.");
        }

        return snapshot;
    }

    /// <summary>
    /// Parse com duas guardas que o hash sozinho não dá: <b>sem chave duplicada</b> e
    /// <b>canônico</b>. Reserializar o que foi lido tem de reproduzir os bytes — um
    /// payload com chaves fora de ordem, com espaços, ou com uma chave repetida (de que
    /// o parser escolheria uma) não é um envelope canônico, é outra coisa com o mesmo
    /// hash recomputado por quem o adulterou.
    /// </summary>
    /// <remarks>
    /// <para>
    /// O <paramref name="perfil"/> é do <b>chamador</b>, não fixo neste método: "estar na
    /// forma canônica" é uma pergunta que só tem resposta relativa às regras de bytes de um
    /// perfil, e fixar aqui o perfil corrente impediria testar o parse sob outro perfil (ver
    /// <c>RegistroCodecsEnvelopePerfilTests</c>) ou reaproveitá-lo caso uma versão futura,
    /// pós-produção, emita sob perfil distinto — depois de ter passado, corretamente, pelo
    /// gate do registro.
    /// </para>
    /// <para>
    /// A checagem de forma repete a do <see cref="RegistroCodecsEnvelope"/> de propósito:
    /// <c>Decodificar</c> é público e chamável direto, sem passar pelo registro.
    /// </para>
    /// </remarks>
    internal static Result<JsonObject> Parsear(IPerfilCanonico perfil, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(perfil);

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(bytes, nodeOptions: null, OpcoesDocumento);
        }
        catch (JsonException excecao)
        {
            return Result<JsonObject>.Failure(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"Os bytes congelados não são um JSON válido e sem chaves duplicadas: {excecao.Message}"));
        }

        if (node is not JsonObject payload)
        {
            return Result<JsonObject>.Failure(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                "Os bytes congelados não são um objeto JSON."));
        }

        byte[] recanonicalizado;
        try
        {
            recanonicalizado = perfil.Serializar(payload);
        }
        catch (PayloadForaDoPerfilCanonicoException excecao)
        {
            return Result<JsonObject>.Failure(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"Os bytes congelados contêm o que o perfil '{perfil.Algoritmo}' não representa: {excecao.Message}"));
        }

        if (!recanonicalizado.AsSpan().SequenceEqual(bytes))
        {
            return Result<JsonObject>.Failure(new DomainError(
                ErrosCodecEnvelope.IntegridadeViolada,
                "Os bytes congelados não estão na forma canônica (ADR-0100) — reserializá-los produz bytes distintos."));
        }

        return Result<JsonObject>.Success(payload);
    }

    public Result<EnvelopeReidratado> Decodificar(VersaoConfiguracao versao)
    {
        ArgumentNullException.ThrowIfNull(versao);

        Result<JsonObject> parse = Parsear(Perfil, versao.ConfiguracaoCongeladaCanonica);
        if (parse.IsFailure)
        {
            return Result<EnvelopeReidratado>.Failure(parse.Error!);
        }

        JsonObject payload = parse.Value!;
        LeitorEnvelope leitor = new();

        bool temRetificacao = payload.ContainsKey("retificacao");
        string[] chavesEsperadas = temRetificacao
            ? [.. BlocosReais, "retificacao"]
            : BlocosReais;

        leitor.ExigirChaves(payload, "$", chavesEsperadas);

        LerTipoProcesso(leitor, payload);

        DadosEdital? dados = LerDadosEdital(leitor, payload, out string hashDocumento);
        IReadOnlyList<EtapaProcesso> etapas = LerEtapas(leitor, payload);
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicao = LerDistribuicao(leitor, payload);
        OfertaAtendimentoEspecializado? atendimento = LerAtendimento(leitor, payload);
        ConfiguracaoBonusRegional? bonus = LerBonusRegional(leitor, payload);
        ConfiguracaoCascataRemanejamento? cascata = LerCascataRemanejamento(leitor, payload);
        IReadOnlyList<CriterioDesempate> desempate = LerCriteriosDesempate(leitor, payload);
        ConfiguracaoClassificacao? classificacao = LerClassificacao(leitor, payload);
        IReadOnlyList<FaseCronograma> cronogramaFases = LerCronogramaFases(leitor, payload, comId: true);
        LerIdentidadesUnidade(leitor, payload);
        (ResultadoConformidade? conformidade, IReadOnlyList<DocumentoExigido> documentosExigidos, ReferenciaTemporalFatos? referenciaTemporalFatos,
            IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadadosFatosCongelados) = LerDocumentosExigidos(leitor, payload);
        (string? formularioTitulo, string? formularioTermoAceiteTexto) = LerFormulario(leitor, payload);
        ConfiguracaoDivulgacao? configuracaoDivulgacao = LerDivulgacao(leitor, payload);
        ConfiguracaoTaxaInscricao? configuracaoTaxaInscricao = LerTaxaInscricao(leitor, payload);
        (LocalidadeRegente? localidade, string? fusoHorario) = LerLocalidade(leitor, payload);
        ReferenciaRegra? algoritmoContagemPrazo = LerAlgoritmoContagemPrazo(leitor, payload);
        CalendarioDiasUteisCongelado? calendarioDiasUteis = LerCalendarioDiasUteis(leitor, payload, cronogramaFases);
        RetificacaoInfo? retificacao = temRetificacao ? LerRetificacao(leitor, payload) : null;

        if (leitor.Falhou)
        {
            return leitor.Falha<EnvelopeReidratado>();
        }

        Dictionary<Guid, DocumentoExigido> exigenciasPorId = IndexarExigenciasPorId(leitor, documentosExigidos);
        if (leitor.Falhou)
        {
            return leitor.Falha<EnvelopeReidratado>();
        }

        IReadOnlyList<NoExigencia> raizes = LerArvoreSatisfacao(leitor, payload, exigenciasPorId);
        if (leitor.Falhou)
        {
            return leitor.Falha<EnvelopeReidratado>();
        }

        (IReadOnlyList<FatoColetado> Fatos, IReadOnlyDictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> ValoresSelecionaveis)
            fatosColetadosLidos = LerFatosColetados(leitor, payload);
        IReadOnlyList<FatoColetado> fatosColetados = fatosColetadosLidos.Fatos;
        IReadOnlyList<ConfiguracaoDerivacaoFato> regrasDerivacao = LerRegrasDerivacao(leitor, payload);
        string versaoInterpretador = leitor.TextoNaoVazio(payload, "versaoInterpretador", "$");
        IReadOnlyList<string> modalidadesOfertadas = leitor.Textos(payload, "modalidadesOfertadas", "$");
        if (leitor.Falhou)
        {
            return leitor.Falha<EnvelopeReidratado>();
        }

        if (VerificarCoerenciaComAVersao(versao, hashDocumento, retificacao) is { } incoerencia)
        {
            return Result<EnvelopeReidratado>.Failure(incoerencia);
        }

        IReadOnlyList<NoExigencia> todosOsNos = [.. raizes.SelectMany(static raiz => raiz.AchatarComDescendentes())];

        // Fail-closed do bloco de coleta/derivação (RN08): um envelope adulterado que declare fato
        // duplicado, cite fato inexistente num gatilho/pré-condição/regra, contribua código fora do
        // domínio de modalidades, feche ciclo no grafo conjunto, ou cujo grafo/modalidades congelados
        // divirjam do recomputado, é recusado como malformado — nunca reidratado como se fosse íntegro.
        if (ValidarBlocoDeFatosEDerivacao(
            fatosColetados, regrasDerivacao, documentosExigidos,
            versaoInterpretador, modalidadesOfertadas, distribuicao, payload) is { } malformado)
        {
            return Result<EnvelopeReidratado>.Failure(malformado);
        }

        GrafoConfiguracao grafo = new(
            etapas, atendimento!, distribuicao, bonus, desempate, classificacao!, cronogramaFases,
            documentosExigidos, todosOsNos, referenciaTemporalFatos, fatosColetados, regrasDerivacao,
            cascataRemanejamento: cascata,
            formularioTitulo: formularioTitulo,
            formularioTermoAceiteTexto: formularioTermoAceiteTexto,
            configuracaoDivulgacao: configuracaoDivulgacao,
            configuracaoTaxaInscricao: configuracaoTaxaInscricao,
            localidade: localidade,
            algoritmoContagemPrazo: algoritmoContagemPrazo);
        return Result<EnvelopeReidratado>.Success(
            new EnvelopeReidratado(
                grafo, dados!, hashDocumento, fusoHorario!, retificacao, conformidade,
                metadadosFatosCongelados, fatosColetadosLidos.ValoresSelecionaveis, calendarioDiasUteis));
    }

    /// <summary>
    /// <c>documentosExigidos.exigencias</c> nunca tem <c>exigenciaId</c> duplicado quando
    /// produzido por um encoder real — mas um envelope adulterado poderia ter. Sem esta
    /// checagem, <c>ToDictionary</c> lançaria <see cref="ArgumentException"/> (500 não
    /// tratado) em vez de recusar como envelope malformado, mesma disciplina do restante do
    /// decoder.
    /// </summary>
    private static Dictionary<Guid, DocumentoExigido> IndexarExigenciasPorId(
        LeitorEnvelope leitor, IReadOnlyList<DocumentoExigido> documentosExigidos)
    {
        List<Guid> duplicados = [.. documentosExigidos
            .GroupBy(static d => d.Id)
            .Where(static grupo => grupo.Count() > 1)
            .Select(static grupo => grupo.Key)];

        if (duplicados.Count > 0)
        {
            return leitor.Propagar<Dictionary<Guid, DocumentoExigido>>(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"'documentosExigidos.exigencias': o exigenciaId '{duplicados[0]}' aparece mais de uma vez.")) ?? [];
        }

        return documentosExigidos.ToDictionary(static d => d.Id);
    }
}
