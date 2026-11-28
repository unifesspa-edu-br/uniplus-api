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
/// Lê o envelope corrente, confere sua forma canônica e reconstrói os blocos publicados.
/// </summary>
public sealed class EnvelopeCodec : IRegistroCodecsEnvelope
{
    private static readonly string[] BlocosReais =
    [
        "motivosDeDecisao",
        "provaPresencial",
        "importacaoDoResultado",
        "cadastroDeReserva",
        "manifestacaoDeInteresse",
        "tipoProcesso",
        "periodo",
        "etapas",
        "distribuicao",
        "modalidades",
        "ofertas",
        "atendimento",
        "fada",
        "ordemDeRemanejamento",
        "criteriosDesempate",
        "classificacao",
        "hashesEdital",
        "cronogramaFases",
        "documentosExigidos",
        "vagas",
        "regrasDeComprovacao",
        "formularios",
        "divulgacao",
        "identidadesUnidade",
        "dadosDosFormularios",
        "gruposDosFormularios",
        "calculosDosDados",
        "dependenciasEntreItens",
        "versaoDasRegrasDeCalculo",
        "modalidadesOfertadas",
        "taxaInscricao",
        "localidade",
        "opcoesDeCurso",
        "algoritmoContagemPrazo",
        "calendarioDiasUteis",
        "identificadorLegivel",
        "nome",
        "dadosCitadosSemPergunta",
        "tiposDeAto",
        "dadosCalculados",
    ];

    /// <summary>
    /// Chave duplicada é <b>erro</b>, não “a última ganha”. Um envelope com
    /// <c>"fator"</c> duas vezes tem duas leituras possíveis, e a que o hash cobre é
    /// indistinguível da que o parser escolheria.
    /// </summary>
    private static readonly JsonDocumentOptions OpcoesDocumento = new() { AllowDuplicateProperties = false };

    /// <summary>Recusa JSON inválido, chaves repetidas e bytes fora da forma canônica.</summary>
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
        try
        {
            return ComoObjeto(JsonNode.Parse(bytes, nodeOptions: null, OpcoesDocumento));
        }
        catch (JsonException)
        {
            return Falha(
                ErrosCodecEnvelope.EnvelopeMalformado,
                "Os bytes congelados devem ser um JSON válido e sem chaves duplicadas.");
        }
    }

    private static Result<JsonObject> ComoObjeto(JsonNode? node) =>
        node is JsonObject payload
            ? Result<JsonObject>.Success(payload)
            : Falha(ErrosCodecEnvelope.EnvelopeMalformado, "Os bytes congelados não são um objeto JSON.");

    private static DomainError? ConferirFormaCanonica(IPerfilCanonico perfil, JsonObject payload, byte[] bytes)
    {
        try
        {
            return ConferirIgualdade(perfil.Serializar(payload), bytes);
        }
        catch (PayloadForaDoPerfilCanonicoException)
        {
            return new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                "Os bytes congelados contêm dados que o perfil canônico não representa.");
        }
    }

    private static DomainError? ConferirIgualdade(byte[] recanonicalizado, byte[] bytes) =>
        recanonicalizado.AsSpan().SequenceEqual(bytes)
            ? null
            : new DomainError(
                ErrosCodecEnvelope.IntegridadeViolada,
                "Os bytes congelados não estão na forma canônica (ADR-0100) — reserializá-los produz bytes distintos.");

    private static Result<JsonObject> Falha(string codigo, string mensagem) =>
        Result<JsonObject>.Failure(new DomainError(codigo, mensagem));

    public bool LeAVersao(string schemaVersion) =>
        string.Equals(schemaVersion, SnapshotPublicacaoCanonicalizer.SchemaVersionAtual, StringComparison.Ordinal);

    public Result<EnvelopeReidratado> Reidratar(VersaoConfiguracao versao)
    {
        ArgumentNullException.ThrowIfNull(versao);
        if (ConferirVersaoEvidencia(versao) is { } erro)
        {
            return Result<EnvelopeReidratado>.Failure(erro);
        }

        Result<EnvelopeReidratado> lido = Decodificar(versao);
        return lido.IsFailure ? lido : ConferirCoberturaDosDocumentos(lido.Value!);
    }

    private DomainError? ConferirVersaoEvidencia(VersaoConfiguracao versao) =>
        LeAVersao(versao.SchemaVersion)
            ? ConferirEvidencia(versao)
            : new DomainError(ErrosCodecEnvelope.VersaoDesconhecida,
                "A versão do envelope não é reconhecida.");

    private static DomainError? ConferirEvidencia(VersaoConfiguracao versao)
    {
        PerfilCanonicoV1 perfil = PerfilCanonicoV1.Instancia;
        if (!string.Equals(versao.AlgoritmoHash, perfil.Algoritmo, StringComparison.Ordinal))
        {
            return new DomainError(ErrosCodecEnvelope.AlgoritmoNaoSuportado,
                "A versão de configuração declara um algoritmo de hash não suportado.");
        }

        return string.Equals(perfil.HashHex(versao.ConfiguracaoCongeladaCanonica), versao.HashConfiguracao, StringComparison.Ordinal)
            ? null
            : new DomainError(ErrosCodecEnvelope.IntegridadeViolada, "Os bytes congelados não produzem o hash registrado na versão de configuração.");
    }

    // Cada exigência precisa estar associada a uma regra de documento na configuração publicada.
    private static Result<EnvelopeReidratado> ConferirCoberturaDosDocumentos(EnvelopeReidratado envelope)
    {
        ConfiguracaoPublicada configuracao = envelope.Configuracao;
        DocumentoExigido? orfa = ExigenciaSemRegraDeDocumento(configuracao);
        if (orfa is not null)
        {
            return Result<EnvelopeReidratado>.Failure(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                "Uma exigência documental não está associada a uma regra de documento na configuração publicada."));
        }

        return Result<EnvelopeReidratado>.Success(envelope);
    }

    private static DocumentoExigido? ExigenciaSemRegraDeDocumento(ConfiguracaoPublicada configuracao)
    {
        HashSet<Guid> cobertas = [.. configuracao.Exigencias.RegrasDeComprovacao
            .Where(static n => n.Forma == FormaDeComprovacao.Documento)
            .Select(static n => n.DocumentoExigidoId!.Value)];
        return configuracao.Exigencias.DocumentosExigidos.FirstOrDefault(d => !cobertas.Contains(d.Id));
    }

    private static Result<EnvelopeReidratado> Decodificar(VersaoConfiguracao versao)
    {
        ArgumentNullException.ThrowIfNull(versao);

        Result<JsonObject> parse = Parsear(PerfilCanonicoV1.Instancia, versao.ConfiguracaoCongeladaCanonica);
        if (parse.IsFailure)
        {
            return Result<EnvelopeReidratado>.Failure(parse.Error!);
        }

        return LerBlocos(versao, parse.Value!);
    }

    private static Result<EnvelopeReidratado> LerBlocos(VersaoConfiguracao versao, JsonObject payload)
    {
        LeitorEnvelope leitor = new();

        bool temRetificacao = payload.ContainsKey("retificacao");
        leitor.ExigirChaves(payload, "$", temRetificacao ? [.. BlocosReais, "retificacao"] : BlocosReais);

        DeclaracoesDoProcessoLidas declaracoes = LeituraDasDeclaracoesDoProcesso.Ler(leitor, payload);
        BlocosDoEnvelope? blocos = LeituraDoEnvelope.Ler(leitor, payload, temRetificacao);
        return blocos is null
            ? leitor.Falha<EnvelopeReidratado>()
            : CoerenciaEntreBlocos.Conferir(declaracoes.Aplicar(blocos), versao, payload);
    }
}
