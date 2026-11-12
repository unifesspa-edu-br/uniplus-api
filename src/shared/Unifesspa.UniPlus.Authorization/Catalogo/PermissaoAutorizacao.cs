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
        "Lista todas as permissões da plataforma, agrupadas por módulo e pelo alvo de cada permissão, para quem administra concessões.",
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
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, II, combinado com o art. 23",
        ReservadaAGovernanca = true)]
    ConcessoesAdministrar = 3,

    /// <summary>Aprovar, como gestor de acessos, a adequação funcional do pedido de concessão.</summary>
    [Permissao(
        "autorizacao:pedidos:aprovar-funcional",
        "Aprovar a adequação funcional dos pedidos de concessão",
        "Consulta a fila da primeira etapa dos pedidos de concessão, com o nome e o e-mail de quem pede e de quem recebe o acesso, e aprova ou recusa a adequação funcional.",
        ClassificacaoDeProtecao.Pessoal,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, II, combinado com o art. 23",
        ReservadaAGovernanca = true)]
    PedidosAprovarFuncional = 4,

    /// <summary>Aprovar, como Representante do controlador, a adequação do pedido de concessão à proteção de dados.</summary>
    [Permissao(
        "autorizacao:pedidos:aprovar-protecao-de-dados",
        "Aprovar a adequação dos pedidos de concessão à proteção de dados",
        "Consulta a fila da segunda etapa dos pedidos de concessão, os de perfil com permissão pessoal ou sensível, e aprova ou recusa, em nome da Unifesspa, a adequação à proteção de dados.",
        ClassificacaoDeProtecao.Pessoal,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, II, combinado com o art. 23",
        ReservadaAGovernanca = true)]
    PedidosAprovarProtecaoDeDados = 5,

    /// <summary>Conduzir a revisão periódica dos acessos a dado pessoal ou sensível, como Representante do controlador.</summary>
    [Permissao(
        "autorizacao:revisoes:conduzir",
        "Conduzir a revisão periódica dos acessos a dado pessoal ou sensível",
        "Abre e consulta as revisões, que listam quem tem acesso a dado pessoal ou sensível, e confirma ou revoga cada concessão, exceto as de quem é Representante do controlador.",
        ClassificacaoDeProtecao.Pessoal,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, II, combinado com o art. 23",
        ReservadaAGovernanca = true)]
    RevisoesConduzir = 6,

    /// <summary>Rever, como gestor de acessos, as concessões dos Representantes do controlador na revisão periódica.</summary>
    [Permissao(
        "autorizacao:revisoes:rever-protecao-de-dados",
        "Rever as concessões dos Representantes do controlador na revisão periódica dos acessos",
        "Consulta as revisões e confirma ou revoga as concessões de quem é Representante do controlador, que os Representantes não reveem.",
        ClassificacaoDeProtecao.Pessoal,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, II, combinado com o art. 23",
        ReservadaAGovernanca = true)]
    RevisoesReverProtecaoDeDados = 7,

    /// <summary>Aprovar, como gestão de acessos, a versão proposta de um perfil de acesso.</summary>
    [Permissao(
        "autorizacao:perfis:aprovar-funcional",
        "Aprovar as versões de perfil pela gestão de acessos",
        "Consulta a fila da primeira etapa das versões propostas dos perfis, com quem as criou e as permissões que acrescentam, e aprova ou recusa a versão.",
        ClassificacaoDeProtecao.Pessoal,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, II, combinado com o art. 23",
        ReservadaAGovernanca = true)]
    PerfisAprovarFuncional = 8,

    /// <summary>Aprovar, como Representante do controlador, a versão proposta de um perfil de acesso que acrescenta permissão protegida.</summary>
    [Permissao(
        "autorizacao:perfis:aprovar-protecao-de-dados",
        "Aprovar as versões de perfil na etapa de proteção de dados",
        "Consulta a fila da segunda etapa das versões propostas dos perfis, as que acrescentam permissão pessoal, identificadora ou sensível, lê o documento instituidor e aprova ou recusa a versão.",
        ClassificacaoDeProtecao.Pessoal,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, II, combinado com o art. 23",
        ReservadaAGovernanca = true)]
    PerfisAprovarProtecaoDeDados = 9,

    /// <summary>Publicar uma versão do termo de responsabilidade, como gestor de acessos.</summary>
    [Permissao(
        "autorizacao:termos:publicar",
        "Publicar o termo de responsabilidade",
        "Publica uma nova versão do termo de responsabilidade que o beneficiário aceita antes de receber acesso a dado pessoal ou sensível.",
        ClassificacaoDeProtecao.Interno,
        ReservadaAGovernanca = true)]
    TermosPublicar = 10,

    /// <summary>Consultar, como Encarregado de dados, os pedidos de concessão, sem decidir nenhuma etapa.</summary>
    [Permissao(
        "autorizacao:pedidos:consultar",
        "Consultar os pedidos de concessão",
        "Consulta o histórico de todos os pedidos de concessão, o detalhe, a trilha e os anexos de cada um, com o nome e o e-mail de quem pede e de quem recebe o acesso, sem decidir nenhuma etapa.",
        ClassificacaoDeProtecao.Pessoal,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, II, combinado com o art. 23",
        ReservadaAGovernanca = true)]
    PedidosConsultar = 11,

    /// <summary>Consultar, como Encarregado de dados, as concessões de acesso, sem revogar.</summary>
    [Permissao(
        "autorizacao:concessoes:consultar",
        "Consultar as concessões de acesso",
        "Consulta as concessões, a trilha de cada uma e a da instalação, o que expõe quem tem acesso a quê, por pessoa, sem revogar.",
        ClassificacaoDeProtecao.Pessoal,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, II, combinado com o art. 23",
        ReservadaAGovernanca = true)]
    ConcessoesConsultar = 12,

    /// <summary>Consultar, como Encarregado de dados, as revisões periódicas dos acessos, sem abrir revisão nem decidir item.</summary>
    [Permissao(
        "autorizacao:revisoes:consultar",
        "Consultar as revisões periódicas dos acessos",
        "Consulta as revisões e os itens de cada uma, com quem tem acesso a dado pessoal ou sensível e quem decidiu cada item, sem abrir revisão nem decidir item.",
        ClassificacaoDeProtecao.Pessoal,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, II, combinado com o art. 23",
        ReservadaAGovernanca = true)]
    RevisoesConsultar = 13,

    /// <summary>Consultar, como Encarregado de dados, os perfis e as versões de perfil, sem decidir.</summary>
    [Permissao(
        "autorizacao:perfis:consultar-versoes",
        "Consultar os perfis e as versões de perfil",
        "Consulta os perfis, as versões, os documentos instituidores, as decisões e quem as tomou, a janela de migração, a contenção, as reclassificações e as divergências da governança, sem decidir.",
        ClassificacaoDeProtecao.Pessoal,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, II, combinado com o art. 23",
        ReservadaAGovernanca = true)]
    PerfisConsultarVersoes = 14,

    /// <summary>Solicitar, como Encarregado de dados, providência sobre uma concessão.</summary>
    [Permissao(
        "autorizacao:providencias:solicitar",
        "Solicitar providência sobre uma concessão",
        "Pede, com fundamento, a retirada ou a revisão de uma concessão de acesso, dirigida aos gestores de acessos e aos Representantes do controlador.",
        ClassificacaoDeProtecao.Pessoal,
        BaseLegal = "Lei nº 13.709/2018 (LGPD), art. 7º, II, combinado com o art. 23",
        ReservadaAGovernanca = true)]
    ProvidenciasSolicitar = 15,
}
