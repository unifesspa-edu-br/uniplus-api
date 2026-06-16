namespace Unifesspa.UniPlus.Authorization.Enums;

/// <summary>
/// Origem rastreável de uma concessão efetiva (<c>EffectiveGrant</c>) avaliada
/// pela decisão de autorização (ADR-0078). Distingue o que veio no token do que
/// foi resolvido no servidor — concessões server-side são sempre temporárias e
/// exigem validade explícita.
/// </summary>
public enum FonteGrant
{
    /// <summary>Permissão presente no token de acesso (claim). Pode não ter validade própria — herda a validade do token.</summary>
    Token = 0,

    /// <summary>Vínculo derivado de grupo OIDC resolvido no servidor. Concessão temporária — exige validade explícita.</summary>
    OidcGroupBinding = 1,

    /// <summary>Concessão excepcional fora do grupo padrão, resolvida no servidor. Concessão temporária — exige validade explícita.</summary>
    PermissaoExcecional = 2,
}
