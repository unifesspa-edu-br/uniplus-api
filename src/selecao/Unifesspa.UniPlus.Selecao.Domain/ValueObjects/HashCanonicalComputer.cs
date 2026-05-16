namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Computa o hash SHA-256 canônico de uma <see cref="ObrigatoriedadeLegal"/>
/// per ADR-0058 §"Snapshot-on-bind" e CA-05 da Story #460.
/// </summary>
/// <remarks>
/// <para>
/// Determinismo é invariante: o mesmo conteúdo produz o mesmo hash em qualquer
/// runtime, independente da ordem de declaração das propriedades nos records.
/// Isso é alcançado por <em>JSON canônico</em>:
/// </para>
/// <list type="number">
///   <item>Apenas os campos com semântica de regra entram no payload (audit
///   fields, governance e <c>Hash</c> ficam de fora).</item>
///   <item>Serialização via <see cref="JsonSerializer"/> com camelCase e sem
///   indentação.</item>
///   <item>Reordenação alfabética recursiva das chaves de TODO objeto
///   aninhado (inclusive payloads polimórficos do <see cref="PredicadoObrigatoriedade"/>).</item>
///   <item>Bytes UTF-8 alimentam <see cref="SHA256.HashData(System.ReadOnlySpan{byte})"/>;
///   o resultado é serializado como hex minúsculo (64 chars).</item>
/// </list>
/// <para>
/// O retorno é estável entre processos, máquinas e tempo — pré-requisito da
/// constraint <c>UNIQUE (hash) WHERE is_deleted = false</c> (CA-02) e da
/// auditoria forense do <c>ObrigatoriedadeLegalHistorico</c> (CA-03).
/// </para>
/// </remarks>
public static class HashCanonicalComputer
{
    private const int HashHexLength = 64;

    /// <summary>
    /// Opções canônicas de serialização compartilhadas entre o hash e os
    /// snapshots forensicos (<c>ObrigatoriedadeLegalHistoricoInterceptor</c>,
    /// EF <c>ValueComparer</c> do <c>Predicado</c>): camelCase, ignora
    /// <c>null</c> em opcionais (evita que ausência produza hashes distintos
    /// por presença de "key":null), desliga indentação e força enum como
    /// nome textual via <see cref="JsonStringEnumConverter"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Source of truth única</strong> — reusar essas opções é
    /// invariante: se o snapshot persistido divergir do payload que alimentou
    /// o hash, a reprodutibilidade forense quebra silenciosamente.
    /// </para>
    /// <para>
    /// <see cref="JsonStringEnumConverter"/> garante que <see cref="CategoriaObrigatoriedade"/>
    /// e enums futuros entrem no payload pelo nome textual, não pelo ordinal — a
    /// renumeração de enum (acrescentar valor no meio) não muda o hash de regras
    /// existentes, preservando o histórico de auditoria.
    /// </para>
    /// </remarks>
    public static readonly JsonSerializerOptions CanonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>
    /// Computa o hash canônico de uma regra a partir das suas partes
    /// semânticas. O caller é responsável por passar valores normalizados
    /// (uppercase do <c>tipoEditalCodigo</c>, trim de strings, etc.).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Campos deliberadamente fora do hash</strong> (ADR-0058 §"Snapshot-on-bind"):
    /// </para>
    /// <list type="bullet">
    ///   <item><c>DescricaoHumana</c> — texto de apresentação para o admin;
    ///   corrigir typo não cria nova regra.</item>
    ///   <item><c>AtoNormativoUrl</c> — link de citação documental; mudança
    ///   de URL (DOI, link governamental) não muda a regra avaliada.</item>
    ///   <item><c>Proprietario</c> e <c>AreasDeInteresse</c> — governança
    ///   (ADR-0057), não conteúdo da regra; troca de dono entre áreas não
    ///   instancia "outra" regra. Esses campos entram no snapshot forense
    ///   (<c>ObrigatoriedadeLegalHistorico</c>) e no <c>EditalGovernanceSnapshot</c>
    ///   sem alimentar o hash.</item>
    ///   <item>Audit fields (<c>CreatedAt/By</c>, <c>UpdatedAt/By</c>) e
    ///   <c>Hash</c>/<c>IsDeleted</c> — ruído sem semântica de regra.</item>
    /// </list>
    /// <para>
    /// Adicionar/remover campos desta lista exige amendment do ADR-0058 —
    /// muda a definição de "duas regras com mesmo conteúdo" e, por
    /// consequência, o comportamento da constraint <c>UNIQUE</c> parcial
    /// sobre <c>hash</c>.
    /// </para>
    /// </remarks>
    /// <param name="tipoEditalCodigo">Código do tipo de edital ou <c>"*"</c> para universal.</param>
    /// <param name="categoria">Categoria da regra.</param>
    /// <param name="regraCodigo">Código simbólico da regra (ex.: <c>ETAPA_OBRIGATORIA</c>).</param>
    /// <param name="predicado">Predicado tipado da regra (variante da discriminated union).</param>
    /// <param name="baseLegal">Citação legal (ex.: <c>Lei 14.723/2023 art.2º</c>).</param>
    /// <param name="portariaInternaCodigo">Portaria interna que regulamenta, ou <see langword="null"/>.</param>
    /// <param name="vigenciaInicio">Início de vigência (UTC, sem hora).</param>
    /// <param name="vigenciaFim">Fim de vigência, ou <see langword="null"/> para vigência aberta.</param>
    /// <returns>Hash SHA-256 em hex minúsculo (64 chars).</returns>
    public static string Compute(
        string tipoEditalCodigo,
        CategoriaObrigatoriedade categoria,
        string regraCodigo,
        PredicadoObrigatoriedade predicado,
        string baseLegal,
        string? portariaInternaCodigo,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoEditalCodigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(regraCodigo);
        ArgumentNullException.ThrowIfNull(predicado);
        ArgumentException.ThrowIfNullOrWhiteSpace(baseLegal);

        // Serializa o predicado primeiro com o serializer polimórfico do
        // STJ (atributos [JsonPolymorphic]/[JsonDerivedType] em PredicadoObrigatoriedade)
        // para que o "$tipo" discriminator entre no payload. O resultado vira
        // JsonNode e é canonicalizado junto dos outros campos.
        JsonNode? predicadoNode = JsonSerializer.SerializeToNode(predicado, CanonicalOptions);

        JsonObject payload = new()
        {
            ["baseLegal"] = baseLegal,
            ["categoria"] = JsonValue.Create(categoria.ToString()),
            ["portariaInternaCodigo"] = portariaInternaCodigo,
            ["predicado"] = predicadoNode,
            ["regraCodigo"] = regraCodigo,
            ["tipoEditalCodigo"] = tipoEditalCodigo,
            ["vigenciaFim"] = vigenciaFim is { } fim ? fim.ToString("O", CultureInfo.InvariantCulture) : null,
            ["vigenciaInicio"] = vigenciaInicio.ToString("O", CultureInfo.InvariantCulture),
        };

        JsonNode canonical = CanonicalizeRecursive(payload);

        // Use Utf8JsonWriter for byte-stable output (no environment-dependent
        // line endings, no escape policy variation across runtimes).
        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false }))
        {
            canonical.WriteTo(writer);
        }

        byte[] hash = SHA256.HashData(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
        return Convert.ToHexStringLower(hash);
    }

    /// <summary>
    /// Reordena alfabeticamente as chaves de todos os <see cref="JsonObject"/>
    /// aninhados. Arrays preservam a ordem (a ordem é semântica dentro de
    /// <see cref="ModalidadesMinimas"/>, <see cref="BonusObrigatorio"/>, etc.).
    /// Exposto como API pública para que o
    /// <c>ObrigatoriedadeLegalHistoricoInterceptor</c> aplique a mesma
    /// canonicalização ao payload do snapshot — invariante de
    /// reprodutibilidade forense.
    /// </summary>
    public static JsonNode CanonicalizeRecursive(JsonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        switch (node)
        {
            case JsonObject obj:
                JsonObject ordered = new();
                foreach (KeyValuePair<string, JsonNode?> kvp in obj
                    .OrderBy(static p => p.Key, StringComparer.Ordinal))
                {
                    ordered[kvp.Key] = kvp.Value is null ? null : CanonicalizeRecursive(kvp.Value);
                }
                return ordered;

            case JsonArray arr:
                JsonArray rewritten = [];
                foreach (JsonNode? item in arr)
                {
                    rewritten.Add(item is null ? null : CanonicalizeRecursive(item));
                }
                return rewritten;

            default:
                // Para JsonValue (primitivos), reusar o nó é seguro porque ele
                // ainda não tem parent — o caller acabou de criá-lo via JsonObject
                // literal. Caso seja reparented, .DeepClone garante isolamento.
                return node.Parent is null ? node : node.DeepClone();
        }
    }

    /// <summary>
    /// Sanity check para uso em testes/asserts — valida que a string tem o
    /// shape esperado de um SHA-256 em hex minúsculo.
    /// </summary>
    public static bool IsValidHashShape(string? hash)
    {
        if (hash is null || hash.Length != HashHexLength)
        {
            return false;
        }

        return hash.All(static c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'));
    }
}
