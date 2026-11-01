namespace Unifesspa.UniPlus.Authorization.Projecao;

using System.Reflection;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// A proteção que o DTO administrativo declara nas próprias propriedades, lida do <see cref="ProtegidoComoAttribute"/>:
/// a fonte única da classe de cada campo fixo, de que o handler tira os pares que pede ao projetor e a consulta de cada
/// propriedade.
/// </summary>
public static class ProtecaoDeclaradaNoDto
{
    /// <summary>Os pares (natureza, classificação) distintos que as propriedades do DTO declaram.</summary>
    /// <typeparam name="TDto">O DTO a ler.</typeparam>
    public static IReadOnlyList<(NaturezaDoItem Natureza, ClassificacaoDeProtecao Classificacao)> Pares<TDto>() =>
        [
            .. typeof(TDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(static p => p.GetCustomAttribute<ProtegidoComoAttribute>())
                .OfType<ProtegidoComoAttribute>()
                .Select(static a => (a.Natureza, a.Classificacao))
                .Distinct(),
        ];

    /// <summary>A declaração da propriedade; falha se ela não declara, porque é erro de programação.</summary>
    /// <typeparam name="TDto">O DTO que declara a propriedade.</typeparam>
    /// <param name="propriedade">O nome da propriedade.</param>
    public static ProtegidoComoAttribute Da<TDto>(string propriedade) =>
        typeof(TDto).GetProperty(propriedade)?.GetCustomAttribute<ProtegidoComoAttribute>()
        ?? throw new InvalidOperationException($"A propriedade {typeof(TDto).Name}.{propriedade} não declara [ProtegidoComo].");
}
