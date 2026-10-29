namespace Unifesspa.UniPlus.Authorization.Enums;

/// <summary>
/// Origem rastreável de uma concessão efetiva (<c>EffectiveGrant</c>) avaliada
/// pela decisão de autorização (ADR-0078). O token identifica a pessoa e não
/// concede acesso: toda permissão vem de uma concessão gravada no servidor
/// (ADR-0152).
/// </summary>
public enum FonteGrant
{
    /// <summary>Concessão de perfil gravada no Uni+, nascida de pedido aprovado ou da instalação. Escopo da unidade ou da instituição; a validade é opcional.</summary>
    Concessao = 0,

    /// <summary>Vínculo derivado de grupo OIDC resolvido no servidor. Concessão temporária — exige validade explícita.</summary>
    OidcGroupBinding = 1,

    /// <summary>Concessão excepcional fora do grupo padrão, resolvida no servidor. Concessão temporária — exige validade explícita.</summary>
    PermissaoExcecional = 2,
}
