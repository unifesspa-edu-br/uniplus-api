namespace Unifesspa.UniPlus.Authorization.Projecao;

using System.Text.Json.Serialization;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Base dos itens classificados: guarda a proteção fora do JSON.</summary>
/// <param name="Natureza">Se o item é dado ou arquivo.</param>
/// <param name="Classificacao">A classificação do item.</param>
/// <param name="BaseLegal">A base legal congelada do item.</param>
public abstract record ItemClassificado(
    [property: JsonIgnore] NaturezaDoItem Natureza,
    [property: JsonIgnore] ClassificacaoDeProtecao Classificacao,
    [property: JsonIgnore] string? BaseLegal) : IItemClassificado;
