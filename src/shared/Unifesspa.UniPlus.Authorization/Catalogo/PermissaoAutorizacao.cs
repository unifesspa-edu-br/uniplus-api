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

    /// <summary>Manter os perfis de acesso.</summary>
    [Permissao(
        "autorizacao:perfis:manter",
        "Manter perfis de acesso",
        "Cria, edita, ativa, desativa e consulta os perfis de acesso, que reúnem permissões do catálogo.",
        Sensibilidade.Interna,
        ReservadaAGovernanca = true)]
    PerfisManter = 2,

    /// <summary>Administrar as concessões de acesso: consultar, ver a trilha e revogar.</summary>
    [Permissao(
        "autorizacao:concessoes:administrar",
        "Administrar concessões de acesso",
        "Consulta as concessões e a trilha de cada uma e revoga concessões, o que expõe quem tem acesso a quê, por pessoa.",
        Sensibilidade.Pessoal,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, III",
        ReservadaAGovernanca = true)]
    ConcessoesAdministrar = 3,
}
