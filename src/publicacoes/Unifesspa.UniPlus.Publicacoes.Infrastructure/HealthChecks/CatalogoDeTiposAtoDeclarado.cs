namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.HealthChecks;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// O catálogo de tipos de ato que este repositório declara, lido do mesmo arquivo que o
/// bootstrap aplica nos ambientes.
/// </summary>
/// <remarks>
/// Ler o arquivo do bootstrap, e não uma segunda lista escrita aqui, é o que mantém uma fonte
/// só: uma cópia divergiria da declarada sem que nada acusasse, e o diagnóstico passaria a
/// apontar contradição onde não há.
/// </remarks>
internal static partial class CatalogoDeTiposAtoDeclarado
{
    private const string EmbeddedResourceName =
        "Unifesspa.UniPlus.Publicacoes.Infrastructure.seed-tipos-ato.json";

    private static readonly Lazy<LinhaDeclarada[]> Rows =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>Se a declaração pôde ser lida do assembly.</summary>
    internal static bool IsAvailable => Rows.Value.Length > 0;

    /// <summary>
    /// As linhas que o catálogo declara vigentes **na data pedida**.
    /// </summary>
    /// <remarks>
    /// O catálogo é versionado por vigência, e o mesmo código pode ter mais de uma versão
    /// declarada — uma valendo hoje e a sucessora começando adiante. Achatar o arquivo num valor
    /// por código perderia o período e compararia o cadastro de hoje com uma declaração que ainda
    /// não vale.
    /// </remarks>
    internal static LinhaDeclarada[] VigentesEm(DateOnly data) =>
        [.. Rows.Value.Where(linha => linha.VigenteEm(data))];

    /// <summary>
    /// Recurso ausente ou ilegível devolve vazio, e não exceção. A leitura é memoizada: deixar a
    /// falha escapar a congelaria no <see cref="Lazy{T}"/> para o resto do processo, e cada sonda
    /// seguinte relançaria a mesma — sem o desfecho degradado que o chamador sabe descrever.
    /// </summary>
    private static LinhaDeclarada[] Load()
    {
        using Stream? stream = typeof(CatalogoDeTiposAtoDeclarado).Assembly
            .GetManifestResourceStream(EmbeddedResourceName);

        return stream is null ? [] : Ler(stream);
    }

    internal static LinhaDeclarada[] Ler(Stream stream)
    {
        try
        {
            return JsonSerializer.Deserialize(stream, LinhaDeclaradaContext.Default.LinhaDeclaradaArray) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    internal sealed record LinhaDeclarada
    {
        public required string Codigo { get; init; }
        public required string Nome { get; init; }
        public required bool CongelaConfiguracao { get; init; }
        public required bool UnicoPorObjeto { get; init; }
        public required bool EfeitoIrreversivel { get; init; }
        public required bool EhResultado { get; init; }
        public required bool PublicavelComoAvulso { get; init; }
        public required bool ExigeAtoCorrigido { get; init; }
        public required DateOnly VigenciaInicio { get; init; }
        public required DateOnly? VigenciaFim { get; init; }
        public required string? BaseLegal { get; init; }

        /// <summary>O mesmo intervalo que o agregado aplica: <c>[início, fim)</c>.</summary>
        internal bool VigenteEm(DateOnly data) =>
            VigenciaInicio <= data && (VigenciaFim is null || VigenciaFim > data);
    }

    [JsonSourceGenerationOptions(
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true)]
    [JsonSerializable(typeof(LinhaDeclarada[]))]
    private sealed partial class LinhaDeclaradaContext : JsonSerializerContext;
}
