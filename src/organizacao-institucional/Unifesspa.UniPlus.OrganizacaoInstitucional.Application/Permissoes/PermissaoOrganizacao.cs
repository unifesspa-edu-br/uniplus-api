namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Permissoes;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Permissões do módulo OrganizacaoInstitucional (ADR-0078, ADR-0151). Acrescentar uma permissão é
/// acrescentar um membro com o seu <see cref="PermissaoAttribute"/>; nada mais é mantido
/// em paralelo.
/// </summary>
/// <remarks>
/// O valor numérico do membro não sai do processo: o que circula em registro de decisão,
/// resposta da API e frontend é sempre o código.
/// </remarks>
[ModuloDePermissoes("organizacao")]
public enum PermissaoOrganizacao
{
    /// <summary>Manter cadastro da Instituição.</summary>
    [Permissao(
        "organizacao:instituicao:manter",
        "Manter cadastro da Instituição",
        "Cria, altera e remove os dados da Instituição, que é única.",
        ClassificacaoDeProtecao.Interno)]
    InstituicaoManter = 1,

    /// <summary>Manter cadastro das unidades.</summary>
    [Permissao(
        "organizacao:unidades:manter",
        "Manter cadastro das unidades",
        "Cria, altera e remove as unidades organizacionais da instituição.",
        ClassificacaoDeProtecao.Interno)]
    UnidadesManter = 2,
}
