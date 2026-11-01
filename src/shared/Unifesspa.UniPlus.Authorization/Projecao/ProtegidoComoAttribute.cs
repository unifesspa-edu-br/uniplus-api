namespace Unifesspa.UniPlus.Authorization.Projecao;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Declara a classificação de uma propriedade do DTO administrativo cujo valor é uma coluna fixa do modelo ou um valor
/// derivado dela. A saída da propriedade depende da permissão do par (natureza, classificação); sem decisão permitida
/// na requisição, o serializador a omite (ADR-0153).
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ProtegidoComoAttribute : Attribute
{
    /// <summary>Declara a classificação da propriedade.</summary>
    public ProtegidoComoAttribute(ClassificacaoDeProtecao classificacao) => Classificacao = classificacao;

    /// <summary>A classificação da propriedade.</summary>
    public ClassificacaoDeProtecao Classificacao { get; }

    /// <summary>Se a propriedade é dado ou arquivo; é dado quando omitida.</summary>
    public NaturezaDoItem Natureza { get; init; } = NaturezaDoItem.Dado;

    /// <summary>
    /// A coluna de onde o valor é lido, como <c>schema.tabela.coluna</c> do retrato do modelo. A propriedade lida direto
    /// da coluna não pode ficar abaixo da classificação dela.
    /// </summary>
    public string? Fonte { get; init; }

    /// <summary>
    /// O que transforma o valor lido da <see cref="Fonte"/>, como a máscara do identificador. É o que autoriza a
    /// propriedade a ter classificação abaixo da fonte: o valor derivado vale pela classificação própria.
    /// </summary>
    public string? Transformacao { get; init; }
}
