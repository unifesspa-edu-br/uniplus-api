namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
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
    /// existentes, preservando o histórico de auditoria. O enum que declara código de contrato entra pelo código
    /// declarado, porque o conversor de códigos fica à frente do <see cref="JsonStringEnumConverter"/>.
    /// </para>
    /// </remarks>
    public static readonly JsonSerializerOptions CanonicalOptions = OpcoesJsonDeDocumento.Criar(new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    });

    /// <summary>
    /// Calcula o hash canônico a partir dos campos semânticos da regra.
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
    /// <param name="regra">Regra cuja definição será calculada.</param>
    /// <returns>Hash SHA-256 em hex minúsculo.</returns>
    public static string Compute(ObrigatoriedadeLegal regra)
    {
        ArgumentNullException.ThrowIfNull(regra);
        return HashCanonicalPayload(DefinicaoDaObrigatoriedadeLegal(regra));
    }

    private static JsonObject DefinicaoDaObrigatoriedadeLegal(ObrigatoriedadeLegal regra) => new()
    {
        ["baseLegal"] = regra.BaseLegal,
        ["categoria"] = regra.Categoria.ToString(),
        ["portariaInternaCodigo"] = regra.PortariaInternaCodigo,
        ["predicado"] = JsonSerializer.SerializeToNode(regra.Predicado, CanonicalOptions),
        ["regraCodigo"] = regra.RegraCodigo,
        ["tipoProcessoCodigo"] = regra.TipoProcessoCodigo,
        ["vigenciaFim"] = regra.VigenciaFim is { } fim ? fim.ToString("O", CultureInfo.InvariantCulture) : null,
        ["vigenciaInicio"] = regra.VigenciaInicio.ToString("O", CultureInfo.InvariantCulture),
    };

    /// <summary>
    /// Computa o hash content-addressable de uma entrada do <c>regras_do_catalogo</c>
    /// (<see cref="Entities.RegraCatalogo"/>) sobre a sua definição COMPLETA:
    /// <c>codigo</c>+<c>versao</c>+<c>tipo</c>+<c>declaracao_da_regra</c>+<c>invariantes</c>+<c>base_legal</c>.
    /// Mudar qualquer campo definicional muda o hash — e a versão de uma regra
    /// é imutável (nova semântica = nova versão), o que dá a reprodutibilidade
    /// do freeze do snapshot (congelamento dos parâmetros na publicação, UNI-REQ-0019) por construção.
    /// </summary>
    /// <remarks>
    /// Espelha o hash validado do <c>regras_do_catalogo</c> (Postgres real): os
    /// campos de auditoria (<c>created_at/by</c>) e o próprio <c>hash</c> ficam
    /// de fora; a declaração (objeto) e as invariantes (array) entram canonicalizadas
    /// recursivamente, de modo que a ordem das chaves não altere o resultado.
    /// </remarks>
    public static string ComputeRegraCatalogo(RegraCatalogo regra)
    {
        ArgumentNullException.ThrowIfNull(regra);
        return HashCanonicalPayload(DefinicaoDaRegraDoCatalogo(regra));
    }

    private static JsonObject DefinicaoDaRegraDoCatalogo(RegraCatalogo regra) => new()
    {
        ["baseLegal"] = regra.BaseLegal,
        ["codigo"] = regra.Codigo,
        ["declaracaoDaRegra"] = JsonNode.Parse(regra.DeclaracaoDaRegra.GetRawText()),
        ["invariantes"] = JsonNode.Parse(regra.Invariantes.GetRawText()),
        ["tipo"] = regra.Tipo.ToCodigo(),
        ["versao"] = regra.Versao,
    };

    /// <summary>
    /// Canonicaliza (reordena chaves recursivamente), serializa com
    /// <see cref="Utf8JsonWriter"/> byte-stável e devolve o SHA-256 em hex
    /// minúsculo. Fonte única do passo final de hashing — compartilhada entre
    /// o hash de <c>ObrigatoriedadeLegal</c> e o do <c>regras_do_catalogo</c>.
    /// </summary>
    private static string HashCanonicalPayload(JsonObject payload) =>
        ComputeSha256Hex(ComputeSnapshotBytes(payload));

    /// <summary>
    /// Reordena alfabeticamente as chaves de todos os <see cref="JsonObject"/>
    /// aninhados. Arrays preservam a ordem (a ordem é semântica dentro de
    /// <see cref="ModalidadesMinimas"/>, <see cref="AtendimentoDisponivel"/>, etc.).
    /// Exposto como API pública para que o
    /// <c>ObrigatoriedadeLegalHistoricoInterceptor</c> aplique a mesma
    /// canonicalização ao payload do snapshot — invariante de
    /// reprodutibilidade forense.
    /// </summary>
    public static JsonNode CanonicalizeRecursive(JsonNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return node switch
        {
            JsonObject obj => CanonicalizeObject(obj),
            JsonArray array => CanonicalizeArray(array),
            _ => node.Parent is null ? node : node.DeepClone(),
        };
    }

    private static JsonObject CanonicalizeObject(JsonObject obj)
    {
        JsonObject ordered = new();
        foreach (KeyValuePair<string, JsonNode?> property in obj.OrderBy(static p => p.Key, StringComparer.Ordinal))
        {
            ordered[property.Key] = property.Value is null ? null : CanonicalizeRecursive(property.Value);
        }

        return ordered;
    }

    private static JsonArray CanonicalizeArray(JsonArray array)
    {
        JsonArray rewritten = [];
        foreach (JsonNode? item in array)
        {
            rewritten.Add(item is null ? null : CanonicalizeRecursive(item));
        }

        return rewritten;
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

    /// <summary>
    /// Normaliza uma string de negócio para a forma Unicode NFC (ADR-0100
    /// item 1) — sequências combinantes e formas pré-compostas do mesmo texto
    /// colapsam para os mesmos bytes antes de entrar no payload canônico.
    /// Toda string de negócio (nomes, descrições, textos livres) do snapshot
    /// de publicação passa por aqui antes de ser serializada.
    /// </summary>
    public static string NormalizeNfc(string valor)
    {
        ArgumentNullException.ThrowIfNull(valor);
        return valor.Normalize(NormalizationForm.FormC);
    }

    /// <summary>
    /// Serializa um decimal de negócio com escala declarada e arredondamento
    /// half-even (ADR-0100 item 2) — nunca como <c>number</c> JSON de ponto
    /// flutuante, que reintroduziria ambiguidade de representação entre
    /// runtimes. O caller declara <paramref name="escala"/> por campo (schema
    /// do bloco canônico), não um default global.
    /// </summary>
    public static string SerializeDecimalCanonical(decimal valor, int escala)
    {
        if (escala < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(escala), escala, "Escala decimal não pode ser negativa.");
        }

        decimal arredondado = Math.Round(valor, escala, MidpointRounding.ToEven);
        return arredondado.ToString("F" + escala.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A forma única do instante no envelope do congelamento e no hash do comprovante de inscrição: UTC,
    /// RFC 3339, seis casas fixas de fração de segundo e sufixo <c>Z</c>.
    /// </summary>
    public const string CanonicalInstantFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";

    /// <summary>
    /// Serializa um instante na forma <see cref="CanonicalInstantFormat"/> (ADR-0100 item 3). A sétima casa
    /// da fração é truncada, não arredondada: é a precisão que o relógio do sistema e o banco guardam
    /// (ADR-0165), e a fração não varia entre runtimes porque o relógio já a fixa.
    /// </summary>
    public static string SerializeInstantCanonical(DateTimeOffset instante) =>
        instante.ToUniversalTime().ToString(CanonicalInstantFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Lê um instante escrito por <see cref="SerializeInstantCanonical"/>. Recusa qualquer outra forma: sem
    /// fração, com outro número de casas, com deslocamento no lugar do <c>Z</c>, em minúsculas ou com
    /// espaço no lugar do <c>T</c>. Aceitar variação que o emissor nunca produz tornaria um leitor mais
    /// leniente que o decodificador do envelope e esconderia adulteração; e o texto sem designador de
    /// fuso, numa leitura permissiva, receberia o fuso local de quem lê.
    /// </summary>
    public static bool TryParseInstantCanonical([NotNullWhen(true)] string? texto, out DateTimeOffset instante) =>
        DateTimeOffset.TryParseExact(
            texto, CanonicalInstantFormat, CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out instante)
        && string.Equals(SerializeInstantCanonical(instante), texto, StringComparison.Ordinal);

    /// <summary>
    /// Canonicaliza <paramref name="payload"/> (reordenação recursiva de
    /// chaves) e serializa via <see cref="Utf8JsonWriter"/> byte-estável,
    /// retornando os BYTES — não o hash. É a base do
    /// <c>configuracao_congelada_canonica</c> persistido pelo
    /// <c>VersaoConfiguracao</c> (ADR-0100 item 6): a aplicação produz os
    /// bytes uma vez, na publicação; o hash é derivado deles (nunca
    /// recalculado a partir de uma forma intermediária).
    /// </summary>
    /// <remarks>
    /// <strong>Este método é a implementação do perfil <c>canonical-json/sha256@v1</c></strong>
    /// — o perfil sob o qual todo envelope já publicado foi congelado. Ele não muda: alterar
    /// aqui a ordenação, a tabela de escapes, a forma dos números ou o tratamento de
    /// <c>null</c> reescreveria retroativamente os bytes de certames encerrados, que deixariam
    /// de reproduzir o próprio hash. Um perfil com outras regras se escreve <b>ao lado</b>,
    /// com serialização própria, e vale só para as versões de schema que nascerem depois dele.
    /// </remarks>
    public static byte[] ComputeSnapshotBytes(JsonObject payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        JsonNode canonical = CanonicalizeRecursive(payload);

        using MemoryStream buffer = new();
        using (Utf8JsonWriter writer = new(buffer, new JsonWriterOptions { Indented = false }))
        {
            canonical.WriteTo(writer);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Extrai o hash SHA-256 (hex minúsculo) de bytes já canônicos — usado
    /// tanto pela aplicação ao publicar (sobre os bytes recém-produzidos)
    /// quanto por testes de integração (re-hashear os bytes lidos de volta do
    /// banco deve bater com o hash persistido, ADR-0100 §Confirmação).
    /// </summary>
    public static string ComputeSha256Hex(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
