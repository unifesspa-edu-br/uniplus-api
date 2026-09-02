namespace Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// Identidade viva de cada código de cadastro que os predicados de
/// <c>ObrigatoriedadeLegal</c> referenciam, resolvida pela camada de aplicação e
/// entregue ao domínio como dado.
/// </summary>
/// <remarks>
/// <para>Chega como dado, e não como leitor, porque <see cref="AvaliadorConformidadeLegal"/>
/// é serviço de domínio puro e não consulta cadastro (ADR-0013).</para>
/// <para>A decisão de que duas referências designam o mesmo item de catálogo é tomada por
/// identidade, nunca pelo código (ADR-0129): o código do cadastro é editável, e o índice
/// único que o protege é parcial.</para>
/// <para>O critério de desempate não está aqui: sua contraparte congelada
/// (<c>ReferenciaRegra</c>) não guarda identidade de origem, e não precisa — o catálogo de
/// regras é append-only, e sem edição o código é identidade de fato.</para>
/// </remarks>
/// <param name="TiposDocumento">Código do tipo de documento para o identificador vivo.</param>
/// <param name="Modalidades">Código da modalidade para o identificador vivo.</param>
/// <param name="TiposEtapa">Código do tipo de etapa para o identificador vivo.</param>
/// <param name="TiposDeficiencia">Código do tipo de deficiência para o identificador vivo.</param>
public sealed record IdentidadesDeCadastro(
    IReadOnlyDictionary<string, Guid> TiposDocumento,
    IReadOnlyDictionary<string, Guid> Modalidades,
    IReadOnlyDictionary<string, Guid> TiposEtapa,
    IReadOnlyDictionary<string, Guid> TiposDeficiencia)
{
    /// <summary>Nenhum cadastro resolvido — usado quando não há regra vigente a avaliar.</summary>
    public static IdentidadesDeCadastro Vazio { get; } = new(
        new Dictionary<string, Guid>(StringComparer.Ordinal),
        new Dictionary<string, Guid>(StringComparer.Ordinal),
        new Dictionary<string, Guid>(StringComparer.Ordinal),
        new Dictionary<string, Guid>(StringComparer.Ordinal));
}
