namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O codec do envelope de congelamento — <b>um só</b>. Enquanto não há produção nem certame
/// congelado, o sistema emite e lê uma forma canônica corrente (ver <see cref="SchemaVersion"/>)
/// e a evolui livremente: mudar a forma reescreve a fixture, não gera um encoder congelado ao
/// lado. O versionamento forense — um codec por <c>schema_version</c>, encoders aposentados só
/// quando deixam de ser correntes — volta a valer no primeiro certame publicado em <b>produção</b>,
/// não em qualquer ambiente (ADR-0137, emenda às ADRs 0109 e 0110): processos de homologação são
/// descartáveis e recriados quando a forma muda. Até a primeira publicação em produção, a versão
/// sobe sem manter o codec anterior, e nenhum código legado é mantido.
/// </summary>
/// <remarks>
/// <c>Codificar</c> delega ao <see cref="SnapshotPublicacaoCanonicalizer"/>, a projeção viva —
/// e, sendo o único codec, os dois nunca podem divergir. <c>Decodificar</c> reconstrói o
/// envelope pelos leitores de bloco (as classes <c>Leitura*</c>, uma por assunto)
///) e resolve <c>arvoreSatisfacao</c> contra as exigências
/// já lidas, por Id, sem duplicá-las.
/// </remarks>
public sealed class EnvelopeCodec : IEnvelopeCodec
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
        "formularios",
        "divulgacao",
        "identidadesUnidade",
        "fatosColetados",
        "gruposColetados",
        "regrasDerivacao",
        "grafoDependencia",
        "versaoInterpretador",
        "modalidadesOfertadas",
        "taxaInscricao",
        "localidade",
        "opcoesDeCurso",
        "algoritmoContagemPrazo",
        "calendarioDiasUteis",
        "identificadorLegivel",
        "apresentacaoDosPressupostos",
        "tiposDeAto",
        "fatosDerivados",
    ];

    /// <summary>
    /// Chave duplicada é <b>erro</b>, não “a última ganha”. Um envelope com
    /// <c>"fator"</c> duas vezes tem duas leituras possíveis, e a que o hash cobre é
    /// indistinguível da que o parser escolheria.
    /// </summary>
    private static readonly JsonDocumentOptions OpcoesDocumento = new() { AllowDuplicateProperties = false };

    private readonly SnapshotPublicacaoCanonicalizer _encoder = new();

    public string SchemaVersion => "0.0.42";

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

        Result<JsonObject> lido = LerObjeto(bytes);
        if (lido.IsFailure)
        {
            return lido;
        }

        DomainError? forma = ConferirFormaCanonica(perfil, lido.Value!, bytes);
        return forma is null ? lido : Result<JsonObject>.Failure(forma);
    }

    private static Result<JsonObject> LerObjeto(byte[] bytes)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(bytes, nodeOptions: null, OpcoesDocumento);
        }
        catch (JsonException excecao)
        {
            return Falha(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"Os bytes congelados não são um JSON válido e sem chaves duplicadas: {excecao.Message}");
        }

        return node is JsonObject payload
            ? Result<JsonObject>.Success(payload)
            : Falha(ErrosCodecEnvelope.EnvelopeMalformado, "Os bytes congelados não são um objeto JSON.");
    }

    private static DomainError? ConferirFormaCanonica(IPerfilCanonico perfil, JsonObject payload, byte[] bytes)
    {
        byte[] recanonicalizado;
        try
        {
            recanonicalizado = perfil.Serializar(payload);
        }
        catch (PayloadForaDoPerfilCanonicoException excecao)
        {
            return new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"Os bytes congelados contêm o que o perfil '{perfil.Algoritmo}' não representa: {excecao.Message}");
        }

        return recanonicalizado.AsSpan().SequenceEqual(bytes)
            ? null
            : new DomainError(
                ErrosCodecEnvelope.IntegridadeViolada,
                "Os bytes congelados não estão na forma canônica (ADR-0100) — reserializá-los produz bytes distintos.");
    }

    private static Result<JsonObject> Falha(string codigo, string mensagem) =>
        Result<JsonObject>.Failure(new DomainError(codigo, mensagem));

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
        leitor.ExigirChaves(payload, "$", temRetificacao ? [.. BlocosReais, "retificacao"] : BlocosReais);

        BlocosDoEnvelope? blocos = LeituraDoEnvelope.Ler(leitor, payload, temRetificacao);
        return blocos is null
            ? leitor.Falha<EnvelopeReidratado>()
            : CoerenciaEntreBlocos.Conferir(blocos, versao, payload);
    }
}
