namespace Unifesspa.UniPlus.Geo.Infrastructure.Cep;

/// <summary>
/// Opções do reader do lookup de CEP (cascata determinística, ADR-0090). Separadas
/// do <see cref="GeoCepCacheOptions"/> (cache-aside) por serem comportamento de
/// leitura do banco, não de cache.
/// </summary>
public sealed class GeoCepLookupOptions
{
    public const string SectionName = "Geo:Cep:Lookup";

    /// <summary>
    /// Teto de logradouros materializados por CEP (#705). Um CEP tem cardinalidade
    /// baixa de logradouros na prática — só interessam o primário + alternativos —,
    /// mas um CEP patológico (anomalia da fonte DNE) materializaria todas as linhas
    /// num endpoint <c>[AllowAnonymous]</c>, risco de memória/payload. O teto corta a
    /// materialização; atingi-lo é logado como aviso (não mascara a anomalia).
    /// Default 50. Valores &lt; 1 são tratados como 1 pelo reader (defensivo).
    /// </summary>
    public int MaxLogradourosPorCep { get; init; } = 50;
}
