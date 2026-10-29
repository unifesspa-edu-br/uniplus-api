namespace Unifesspa.UniPlus.Publicacoes.Application.Permissoes;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Permissões do módulo Publicacoes (ADR-0078, ADR-0151). Acrescentar uma permissão é
/// acrescentar um membro com o seu <see cref="PermissaoAttribute"/>; nada mais é mantido
/// em paralelo.
/// </summary>
/// <remarks>
/// O valor numérico do membro não sai do processo: o que circula em registro de decisão,
/// resposta da API e frontend é sempre o código.
/// </remarks>
[ModuloDePermissoes("publicacoes")]
public enum PermissaoPublicacoes
{
    /// <summary>Registrar ato normativo.</summary>
    [Permissao(
        "publicacoes:atos-normativos:registrar",
        "Registrar ato normativo",
        "Registra o ato normativo publicado, em publicação nova ou em retificação de outro ato.",
        ClassificacaoDeProtecao.Interno)]
    AtosNormativosRegistrar = 1,

    /// <summary>Manter catálogo de tipos de ato.</summary>
    [Permissao(
        "publicacoes:tipos-ato:manter",
        "Manter catálogo de tipos de ato",
        "Cria, altera e remove os tipos de ato do catálogo.",
        ClassificacaoDeProtecao.Interno)]
    TiposAtoManter = 2,
}
