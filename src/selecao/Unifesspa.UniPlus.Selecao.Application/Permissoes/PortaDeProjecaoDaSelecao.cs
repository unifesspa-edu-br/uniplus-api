namespace Unifesspa.UniPlus.Selecao.Application.Permissoes;

using Unifesspa.UniPlus.Authorization.Projecao;

/// <summary>
/// A porta pela qual a Seleção responde ao projetor qual é a permissão de consulta de cada par (natureza,
/// classificação): a que o catálogo declara com essa natureza e essa sensibilidade (ADR-0153).
/// </summary>
public static class PortaDeProjecaoDaSelecao
{
    /// <summary>A porta do catálogo da Seleção.</summary>
    public static IPortaDePermissaoPorPar Instancia { get; } = new PortaDePermissaoPorPar<PermissaoSelecao>();
}
