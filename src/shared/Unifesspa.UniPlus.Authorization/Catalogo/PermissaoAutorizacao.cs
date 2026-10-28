namespace Unifesspa.UniPlus.Authorization.Catalogo;

using Unifesspa.UniPlus.Authorization.Enums;

/// <summary>Permissões do próprio módulo de autorização.</summary>
[ModuloDePermissoes("autorizacao")]
public enum PermissaoAutorizacao
{
    /// <summary>Consultar o catálogo de permissões.</summary>
    [Permissao(
        "autorizacao:permissoes:consultar-catalogo",
        "Consultar o catálogo de permissões",
        "Lista todas as permissões da plataforma, agrupadas por módulo e recurso, para quem administra concessões.",
        Sensibilidade.Interna)]
    ConsultarCatalogo = 1,
}
