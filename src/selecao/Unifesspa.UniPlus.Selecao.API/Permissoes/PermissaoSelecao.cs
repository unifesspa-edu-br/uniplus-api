namespace Unifesspa.UniPlus.Selecao.API.Permissoes;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Permissões do módulo Seleção (ADR-0078, ADR-0151). Acrescentar uma permissão é
/// acrescentar um membro com o seu <see cref="PermissaoAttribute"/>; nada mais é mantido
/// em paralelo.
/// </summary>
/// <remarks>
/// O valor numérico do membro não sai do processo: o que circula em registro de decisão,
/// resposta da API e frontend é sempre o código.
/// </remarks>
[ModuloDePermissoes("selecao")]
public enum PermissaoSelecao
{
    /// <summary>Manter os motivos de decisão de isenção.</summary>
    [Permissao(
        "selecao:motivos-decisao-isencao:manter",
        "Manter motivos de decisão de isenção",
        "Cria, edita, ativa e desativa os motivos institucionais de decisão de isenção.",
        ClassificacaoDeProtecao.Interno)]
    MotivosDecisaoIsencaoManter = 1,
}
