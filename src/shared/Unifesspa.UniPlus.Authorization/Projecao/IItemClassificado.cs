namespace Unifesspa.UniPlus.Authorization.Projecao;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Um item de resposta administrativa que carrega a própria proteção: o que ele é, a classe que o resolvedor do
/// módulo lhe deu e a base legal que justifica tratá-lo (ADR-0153). Quem implementa mantém os três fora do
/// JSON: são para decidir a saída, não para sair.
/// </summary>
public interface IItemClassificado
{
    /// <summary>Se o item é dado ou arquivo.</summary>
    NaturezaDoItem Natureza { get; }

    /// <summary>A classificação do item; <see cref="ClassificacaoDeProtecao.Nenhuma"/> vale como sensível.</summary>
    ClassificacaoDeProtecao Classificacao { get; }

    /// <summary>A base legal congelada do item; o item sensível sem ela não sai.</summary>
    string? BaseLegal { get; }
}
