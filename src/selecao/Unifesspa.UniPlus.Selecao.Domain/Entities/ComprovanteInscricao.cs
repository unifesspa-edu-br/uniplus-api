namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O comprovante de uma submissão da <see cref="Inscricao"/> (issue #1858, §4.1 do plano da
/// #1855): cópia do que foi enviado, SEM nenhum número de documento, com o hash de integridade
/// que prova que o comprovante não mudou. O hash é o SHA-256 do JSON canônico do conteúdo
/// (número, instante em segundos UTC, oferta, respostas, derivados, grupos e aceites); qualquer
/// um o recalcula, e não é um código de autenticação.
/// </summary>
/// <remarks>
/// Implementa <see cref="IForensicEntity"/> per ADR-0063: append-only, NÃO herda
/// <see cref="Kernel.Domain.Entities.EntityBase"/> e não carrega soft-delete. Uma correção gera um
/// comprovante NOVO; o anterior nunca muda.
/// </remarks>
public sealed class ComprovanteInscricao : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid InscricaoId { get; private init; }

    public string NumeroInscricao { get; private init; } = string.Empty;

    public DateTimeOffset Instante { get; private init; }

    public string HashIntegridade { get; private init; } = string.Empty;

    public Guid? OfertaCursoId { get; private init; }

    /// <summary>O que o candidato informou, no momento da submissão — sem números de documento.</summary>
    public IReadOnlyDictionary<string, JsonElement> Respostas { get; private init; } =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    /// <summary>O que o sistema derivou das respostas nesta submissão.</summary>
    public IReadOnlyDictionary<string, JsonElement> Derivados { get; private init; } =
        new Dictionary<string, JsonElement>(StringComparer.Ordinal);

    /// <summary>As ocorrências de grupo repetível no momento da submissão — mesma exclusão de documento que <see cref="Respostas"/>.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> Grupos { get; private init; } =
        new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal);

    public IReadOnlyList<AceiteDeTermo> Aceites { get; private init; } = [];

    // Construtor de materialização do EF Core.
    private ComprovanteInscricao() { }

    public static ComprovanteInscricao Emitir(
        Guid inscricaoId,
        string numeroInscricao,
        DateTimeOffset instante,
        Guid? ofertaCursoId,
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, JsonElement> derivados,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos,
        IReadOnlyList<AceiteDeTermo> aceites)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(numeroInscricao);
        ArgumentNullException.ThrowIfNull(respostas);
        ArgumentNullException.ThrowIfNull(derivados);
        ArgumentNullException.ThrowIfNull(grupos);
        ArgumentNullException.ThrowIfNull(aceites);

        Dictionary<string, JsonElement> respostasGravadas = NaFormaGravada(respostas);
        Dictionary<string, JsonElement> derivadosGravados = NaFormaGravada(derivados);
        Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> gruposGravados = grupos.ToDictionary(
            static g => g.Key,
            static g => (IReadOnlyList<OcorrenciaDeGrupoInscricao>)
                [.. g.Value.Select(static o => o with { Respostas = NaFormaGravada(o.Respostas) })],
            StringComparer.Ordinal);

        return new ComprovanteInscricao
        {
            InscricaoId = inscricaoId,
            NumeroInscricao = numeroInscricao,
            Instante = instante,
            HashIntegridade = HashDoConteudo(
                numeroInscricao, instante, ofertaCursoId, respostasGravadas, derivadosGravados, gruposGravados, aceites),
            Grupos = gruposGravados,
            OfertaCursoId = ofertaCursoId,
            Respostas = respostasGravadas,
            Derivados = derivadosGravados,
            Aceites = aceites,
        };
    }

    /// <summary>Recalcula o hash a partir do conteúdo, como faria quem audita o comprovante.</summary>
    public bool IntegridadeConfere() =>
        string.Equals(
            HashIntegridade,
            HashDoConteudo(NumeroInscricao, Instante, OfertaCursoId, Respostas, Derivados, Grupos, Aceites),
            StringComparison.Ordinal);

    /// <summary>
    /// O banco guarda o número do JSON em forma decimal, sem expoente (<c>1e3</c> volta como <c>1000</c>). O
    /// comprovante adota essa forma antes do hash, para que o hash recalculado do que foi gravado seja o mesmo.
    /// </summary>
    private static Dictionary<string, JsonElement> NaFormaGravada(IReadOnlyDictionary<string, JsonElement>? valores) =>
        (valores ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal))
            .ToDictionary(static v => v.Key, static v => NaFormaGravada(v.Value), StringComparer.Ordinal);

    private static JsonElement NaFormaGravada(JsonElement valor)
    {
        using MemoryStream destino = new();
        using (Utf8JsonWriter escritor = new(destino))
        {
            Escrever(valor, escritor);
        }

        using JsonDocument documento = JsonDocument.Parse(destino.ToArray());
        return documento.RootElement.Clone();
    }

    private static void Escrever(JsonElement valor, Utf8JsonWriter escritor)
    {
        switch (valor.ValueKind)
        {
            case JsonValueKind.Object:
                escritor.WriteStartObject();
                foreach (JsonProperty propriedade in valor.EnumerateObject())
                {
                    escritor.WritePropertyName(propriedade.Name);
                    Escrever(propriedade.Value, escritor);
                }

                escritor.WriteEndObject();
                break;
            case JsonValueKind.Array:
                escritor.WriteStartArray();
                foreach (JsonElement item in valor.EnumerateArray())
                {
                    Escrever(item, escritor);
                }

                escritor.WriteEndArray();
                break;
            case JsonValueKind.Number when valor.TryGetDecimal(out decimal numero):
                escritor.WriteNumberValue(numero);
                break;
            default:
                valor.WriteTo(escritor);
                break;
        }
    }

    private static string HashDoConteudo(
        string numeroInscricao,
        DateTimeOffset instante,
        Guid? ofertaCursoId,
        IReadOnlyDictionary<string, JsonElement> respostas,
        IReadOnlyDictionary<string, JsonElement> derivados,
        IReadOnlyDictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> grupos,
        IReadOnlyList<AceiteDeTermo> aceites)
    {
        JsonObject conteudo = new()
        {
            ["numeroInscricao"] = numeroInscricao,
            ["instante"] = HashCanonicalComputer.SerializeInstantCanonical(instante),
            ["ofertaCursoId"] = ofertaCursoId?.ToString(),
            ["respostas"] = Nodo(respostas),
            ["derivados"] = Nodo(derivados),
            ["grupos"] = Nodo(grupos),
            ["aceites"] = Nodo(aceites),
        };

        return HashCanonicalComputer.ComputeSha256Hex(HashCanonicalComputer.ComputeSnapshotBytes(conteudo));
    }

    private static JsonNode? Nodo<T>(T valor) => JsonSerializer.SerializeToNode(valor, HashCanonicalComputer.CanonicalOptions);
}
