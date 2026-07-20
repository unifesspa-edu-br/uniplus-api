namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Registro de codecs do envelope, por versão (ADR-0110 D1) — e o <b>gate forense</b>
/// da reidratação.
/// </summary>
/// <remarks>
/// <para>
/// A reidratação não começa pelo parse. Começa por provar que os bytes <b>são</b> o que
/// a linha diz que eles são: o hash tem de bater com o <c>HashConfiguracao</c>
/// persistido, e o algoritmo com o que o codec daquela versão declara. Bytes adulterados
/// decodificam e recodificam <b>identicamente</b> — o round-trip passaria sem que eles
/// correspondessem mais à evidência. Um documento com peso jurídico que não prova o que
/// diz provar não se reidrata: recusa-se.
/// </para>
/// <para>
/// Acrescentar uma versão aqui é <b>obrigatório</b> — o fitness test recusa uma
/// <c>schema_version</c> corrente sem codec completo, e recusa uma versão registrada
/// sem capacidades declaradas. Uma versão nova sem codec quebra o build, em vez de
/// aparecer como um descarte que falha em produção.
/// </para>
/// </remarks>
public sealed class RegistroCodecsEnvelope : IRegistroCodecsEnvelope
{
    /// <summary>Chave duplicada é <b>erro</b>, não “a última ganha” (ver <see cref="VerificarFormaCanonica"/>).</summary>
    private static readonly JsonDocumentOptions OpcoesDocumento = new() { AllowDuplicateProperties = false };

    private readonly Dictionary<string, IEnvelopeCodec> _codecs;

    public RegistroCodecsEnvelope()
    {
        IEnvelopeCodec[] codecs =
            [new EnvelopeCodecV10(), new EnvelopeCodecV11(), new EnvelopeCodecV12(), new EnvelopeCodecV13(), new EnvelopeCodecV14()];
        _codecs = codecs.ToDictionary(static c => c.SchemaVersion, StringComparer.Ordinal);
        SchemaVersionDeEmissaoCorrente = new EnvelopeCodecV14().SchemaVersion;
    }

    public string SchemaVersionDeEmissaoCorrente { get; }

    public IReadOnlyList<CapacidadeCodec> Capacidades =>
    [
        .. _codecs.Values
            .OrderBy(static c => c.SchemaVersion, StringComparer.Ordinal)
            .Select(static c => new CapacidadeCodec(c.SchemaVersion, c.TemEncoder, c.TemDecoder, c.MotivoDaRecusa)),
    ];

    public Result<EnvelopeReidratado> Reidratar(VersaoConfiguracao versao)
    {
        ArgumentNullException.ThrowIfNull(versao);

        if (!_codecs.TryGetValue(versao.SchemaVersion, out IEnvelopeCodec? codec))
        {
            return Result<EnvelopeReidratado>.Failure(new DomainError(
                ErrosCodecEnvelope.VersaoDesconhecida,
                $"A versão '{versao.SchemaVersion}' do envelope não está no registro de codecs — " +
                $"conhecidas: {string.Join(", ", _codecs.Keys.Order(StringComparer.Ordinal))}."));
        }

        // Reidratável ⟺ encoder E decoder. Decoder sem encoder reidrataria sem PROVAR a
        // fidelidade — e é a prova, não a reconstrução, que impede o descarte silencioso.
        if (!codec.TemDecoder || !codec.TemEncoder)
        {
            return Result<EnvelopeReidratado>.Failure(new DomainError(
                ErrosCodecEnvelope.VersaoNaoReidratavel,
                codec.MotivoDaRecusa ?? $"A versão '{codec.SchemaVersion}' do envelope não é reidratável."));
        }

        if (!string.Equals(versao.AlgoritmoHash, codec.AlgoritmoHash, StringComparison.Ordinal))
        {
            return Result<EnvelopeReidratado>.Failure(new DomainError(
                ErrosCodecEnvelope.AlgoritmoNaoSuportado,
                $"A versão de configuração declara o algoritmo '{versao.AlgoritmoHash}', mas o codec " +
                $"'{codec.SchemaVersion}' emite '{codec.AlgoritmoHash}'."));
        }

        // O gate. Antes de qualquer parse: os bytes produzem o hash persistido?
        string hash = HashCanonicalComputer.ComputeSha256Hex(versao.ConfiguracaoCongeladaCanonica);
        if (!string.Equals(hash, versao.HashConfiguracao, StringComparison.Ordinal))
        {
            return Result<EnvelopeReidratado>.Failure(new DomainError(
                ErrosCodecEnvelope.IntegridadeViolada,
                "Os bytes congelados não produzem o hash registrado na versão de configuração — " +
                "a evidência não prova o que diz provar, e não se reidrata configuração a partir dela."));
        }

        if (VerificarFormaCanonica(versao.ConfiguracaoCongeladaCanonica) is { } malformado)
        {
            return Result<EnvelopeReidratado>.Failure(malformado);
        }

        Result<EnvelopeReidratado> decodificado = codec.Decodificar(versao);
        return decodificado.IsFailure ? decodificado : SincronizarArvoreComDocumentosExigidos(decodificado.Value!);
    }

    /// <summary>
    /// O terceiro gate, específico da árvore de satisfação (Story #923). Toda versão
    /// anterior à <c>1.4</c> nunca serializou <c>arvoreSatisfacao</c>:
    /// o decoder dela devolve <see cref="GrafoConfiguracao.NosExigencia"/> sempre vazio, mesmo
    /// quando <c>documentosExigidos.exigencias</c> tem itens reais. Sem este passo, restaurar
    /// uma versão legada e republicá-la sob o encoder corrente emitiria
    /// <c>arvoreSatisfacao: []</c> enquanto <c>documentosExigidos.exigencias</c> continua
    /// populado — o resolvedor de satisfação (que opera sobre a árvore, não sobre a lista
    /// plana) veria zero obrigações documentais para um processo que na verdade tem
    /// exigências vivas.
    /// </summary>
    /// <remarks>
    /// Duas formas de árvore ausente/incompleta, dois tratamentos. Árvore TOTALMENTE vazia —
    /// o único caso que um decoder real produz, sempre que a versão é anterior à 1.4 —
    /// sintetiza o modelo achatado pré-Story #920 (uma raiz-folha por exigência, sem grupo:
    /// o degenerado que já era publicável desde sempre, <see cref="NoExigencia.SintetizarRaizesLegadas"/>).
    /// Árvore PARCIALMENTE incompleta (alguma exigência sem folha correspondente) nunca sai de
    /// um encoder real — só é alcançável por adulteração dos bytes — e é recusada como envelope
    /// malformado, nunca preenchida por adivinhação.
    /// </remarks>
    private static Result<EnvelopeReidratado> SincronizarArvoreComDocumentosExigidos(EnvelopeReidratado envelope)
    {
        GrafoConfiguracao grafo = envelope.Grafo;
        if (grafo.DocumentosExigidos.Count == 0)
        {
            return Result<EnvelopeReidratado>.Success(envelope);
        }

        if (grafo.NosExigencia.Count == 0)
        {
            GrafoConfiguracao grafoComRaizesLegadas = new(
                grafo.Etapas, grafo.OfertaAtendimento, grafo.DistribuicaoVagas, grafo.BonusRegional,
                grafo.CriteriosDesempate, grafo.Classificacao, grafo.CronogramaFases, grafo.DocumentosExigidos,
                NoExigencia.SintetizarRaizesLegadas(grafo.DocumentosExigidos), grafo.ReferenciaTemporalFatos);

            return Result<EnvelopeReidratado>.Success(new EnvelopeReidratado(
                grafoComRaizesLegadas, envelope.Dados, envelope.HashDocumento, envelope.Retificacao,
                envelope.Conformidade, envelope.MetadadosFatosCongelados));
        }

        HashSet<Guid> cobertas = [.. grafo.NosExigencia
            .Where(static n => n.Tipo == TipoNo.Folha)
            .Select(static n => n.DocumentoExigidoId!.Value)];
        DocumentoExigido? orfa = grafo.DocumentosExigidos.FirstOrDefault(d => !cobertas.Contains(d.Id));
        if (orfa is not null)
        {
            return Result<EnvelopeReidratado>.Failure(new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"A exigência '{orfa.TipoDocumentoCodigo}' ({orfa.Id}) não é folha de nenhuma raiz em " +
                "'arvoreSatisfacao' — toda exigência tem de ser folha de exatamente uma árvore."));
        }

        return Result<EnvelopeReidratado>.Success(envelope);
    }

    /// <summary>
    /// O segundo gate, e ele vale para <b>todo</b> codec — presente e futuro. Por isso vive
    /// aqui, e não dentro de um deles.
    /// </summary>
    /// <remarks>
    /// <para>
    /// O hash prova que os bytes são os que <b>foram gravados</b>. Não prova que eles são um
    /// <b>envelope</b>: quem adultera a coluna e recomputa o hash da linha passa pelo
    /// primeiro gate inteiro. O que sobra é a <b>forma</b> — e ela é verificável sem
    /// conhecer versão nenhuma: um envelope canônico (ADR-0100) é, por definição, o que
    /// <c>ComputeSnapshotBytes</c> produz. Reserializar o que se leu tem de <b>reproduzir os
    /// bytes</b>.
    /// </para>
    /// <para>
    /// Isso recusa, de uma vez, chaves fora de ordem, espaços, e a chave <b>duplicada</b> —
    /// que teria duas leituras possíveis, ambas cobertas pelo mesmo hash. O parse ainda
    /// recusa duplicata explicitamente (redundância deliberada: uma versão futura poderia
    /// ter forma canônica diferente, e a garantia não pode depender disso).
    /// </para>
    /// </remarks>
    private static DomainError? VerificarFormaCanonica(byte[] bytes)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(bytes, nodeOptions: null, OpcoesDocumento);
        }
        catch (JsonException excecao)
        {
            return new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"Os bytes congelados não são um JSON válido e sem chaves duplicadas: {excecao.Message}");
        }

        if (node is not JsonObject payload)
        {
            return new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                "Os bytes congelados não são um objeto JSON.");
        }

        return HashCanonicalComputer.ComputeSnapshotBytes(payload).AsSpan().SequenceEqual(bytes)
            ? null
            : new DomainError(
                ErrosCodecEnvelope.IntegridadeViolada,
                "Os bytes congelados não estão na forma canônica (ADR-0100) — reserializá-los produz bytes distintos.");
    }

    public Result<SnapshotCanonico> Recodificar(string schemaVersion, EntradaCanonicalizacao entrada)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaVersion);
        ArgumentNullException.ThrowIfNull(entrada);

        if (!_codecs.TryGetValue(schemaVersion, out IEnvelopeCodec? codec))
        {
            return Result<SnapshotCanonico>.Failure(new DomainError(
                ErrosCodecEnvelope.VersaoDesconhecida,
                $"A versão '{schemaVersion}' do envelope não está no registro de codecs."));
        }

        if (!codec.TemEncoder)
        {
            return Result<SnapshotCanonico>.Failure(new DomainError(
                ErrosCodecEnvelope.VersaoNaoReidratavel,
                codec.MotivoDaRecusa ?? $"A versão '{schemaVersion}' do envelope não tem encoder."));
        }

        return Result<SnapshotCanonico>.Success(codec.Codificar(entrada));
    }
}
