namespace Unifesspa.UniPlus.Authorization.Enums;

/// <summary>
/// Origem rastreável do acesso registrado. As três primeiras são as fontes de uma concessão efetiva
/// (<c>EffectiveGrant</c>) avaliada pela decisão de autorização (ADR-0078): o token identifica a pessoa e não concede
/// acesso, e toda permissão vem de uma concessão gravada no servidor (ADR-0152). A titularidade não é concessão: só
/// aparece no registro da leitura que a parte faz do que é seu (ADR-0166).
/// </summary>
public enum FonteGrant
{
    /// <summary>Concessão de perfil gravada no Uni+, nascida de pedido aprovado ou da instalação. Escopo da unidade ou da instituição; a validade é opcional.</summary>
    Concessao = 0,

    /// <summary>Vínculo derivado de grupo OIDC resolvido no servidor. Concessão temporária — exige validade explícita.</summary>
    OidcGroupBinding = 1,

    /// <summary>Concessão excepcional fora do grupo padrão, resolvida no servidor. Concessão temporária — exige validade explícita.</summary>
    PermissaoExcecional = 2,

    /// <summary>
    /// Acesso da parte ao que é seu, sem concessão, como a leitura do próprio pedido de concessão (ADR-0166). Só o
    /// verificador de acesso a cria, para o registro dizer de onde veio o acesso. Nunca está nas concessões efetivas do
    /// sujeito e nunca autoriza no ponto de decisão: sem unidade nem escopo institucional, não alcançaria resource algum.
    /// </summary>
    Titularidade = 3,
}
