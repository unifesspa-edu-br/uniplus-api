namespace Unifesspa.UniPlus.Authorization.Catalogo;

using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>Permissões do próprio módulo de autorização.</summary>
[ModuloDePermissoes("autorizacao")]
public enum PermissaoAutorizacao
{
    /// <summary>Consultar o catálogo de permissões.</summary>
    [Permissao(
        "autorizacao:permissoes:consultar-catalogo",
        "Consultar o catálogo de permissões",
        "Lista todas as permissões da plataforma, agrupadas por módulo e recurso, para quem administra concessões.",
        ClassificacaoDeProtecao.Interno)]
    ConsultarCatalogo = 1,

    /// <summary>Manter os perfis de acesso.</summary>
    [Permissao(
        "autorizacao:perfis:manter",
        "Manter perfis de acesso",
        "Cria, edita, ativa, desativa e consulta os perfis de acesso, que reúnem permissões do catálogo.",
        ClassificacaoDeProtecao.Interno,
        ReservadaAGovernanca = true)]
    PerfisManter = 2,

    /// <summary>Administrar as concessões de acesso: consultar, ver a trilha e revogar.</summary>
    [Permissao(
        "autorizacao:concessoes:administrar",
        "Administrar concessões de acesso",
        "Consulta as concessões e a trilha de cada uma e revoga concessões, o que expõe quem tem acesso a quê, por pessoa.",
        ClassificacaoDeProtecao.Pessoal,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, III",
        ReservadaAGovernanca = true)]
    ConcessoesAdministrar = 3,

    /// <summary>Aprovar, como gestor de acessos, a adequação funcional do pedido de concessão.</summary>
    [Permissao(
        "autorizacao:pedidos:aprovar-funcional",
        "Aprovar a adequação funcional dos pedidos de concessão",
        "Consulta a fila da primeira etapa dos pedidos de concessão, com o nome e o e-mail de quem pede e de quem recebe o acesso, e aprova ou recusa a adequação funcional.",
        ClassificacaoDeProtecao.Pessoal,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, III",
        ReservadaAGovernanca = true)]
    PedidosAprovarFuncional = 4,

    /// <summary>Aprovar, como encarregada de dados, a adequação do pedido de concessão à proteção de dados.</summary>
    [Permissao(
        "autorizacao:pedidos:aprovar-protecao-de-dados",
        "Aprovar a adequação dos pedidos de concessão à proteção de dados",
        "Consulta a fila da segunda etapa dos pedidos de concessão, os de perfil com permissão pessoal ou sensível, e aprova ou recusa a adequação à proteção de dados.",
        ClassificacaoDeProtecao.Pessoal,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, III",
        ReservadaAGovernanca = true)]
    PedidosAprovarProtecaoDeDados = 5,
}
