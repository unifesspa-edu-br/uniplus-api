namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Status de uma <see cref="Entities.DocumentoExigidoBaseLegal"/> (Story #554, PR #898,
/// issue #549, ADR-0074/ADR-0070). Somente bases <see cref="Resolvido"/> contam para o
/// gate de publicação (<c>ValidadorBaseLegalExigencias</c>) e para a materialização do
/// bloco congelado (PR #903, #548) — <see cref="Pendente"/> é rascunho.
/// </summary>
public enum StatusBaseLegal
{
    Nenhuma = 0,
    Pendente = 1,
    Resolvido = 2,
}
