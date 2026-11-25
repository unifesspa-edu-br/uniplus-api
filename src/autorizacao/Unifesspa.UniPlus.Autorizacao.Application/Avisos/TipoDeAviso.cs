namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Os avisos da governança de perfis que a aplicação emite.</summary>
public enum TipoDeAviso
{
    /// <summary>Permissões foram removidas de um perfil e a remoção já vale.</summary>
    [CodigoDeContrato("PERMISSOES_REMOVIDAS", "Permissões Removidas")]
    PermissoesRemovidas = 1,

    /// <summary>A versão pendente foi cancelada pela contenção de uma remoção.</summary>
    [CodigoDeContrato("VERSAO_CANCELADA_PELA_REMOCAO", "Versão Cancelada Pela Remoção")]
    VersaoCanceladaPelaRemocao = 2,

    /// <summary>As aprovações dadas a uma versão foram descartadas pela mudança de classe.</summary>
    [CodigoDeContrato("APROVACOES_DESCARTADAS", "Aprovações Descartadas")]
    AprovacoesDescartadas = 3,

    /// <summary>Abriu a revisão das concessões do perfil.</summary>
    [CodigoDeContrato("REVISAO_DAS_CONCESSOES_ABERTA", "Revisão Das Concessões Aberta")]
    RevisaoDasConcessoesAberta = 4,

    /// <summary>Faltam 48 horas para o fim da revisão das concessões.</summary>
    [CodigoDeContrato("REVISAO_DAS_CONCESSOES_FALTAM_48_HORAS", "Revisão Das Concessões Faltam 48 Horas")]
    RevisaoDasConcessoesFaltam48Horas = 5,

    /// <summary>Faltam 24 horas para o fim da revisão das concessões.</summary>
    [CodigoDeContrato("REVISAO_DAS_CONCESSOES_FALTAM_24_HORAS", "Revisão Das Concessões Faltam 24 Horas")]
    RevisaoDasConcessoesFaltam24Horas = 6,

    /// <summary>Concessões ficaram retidas, à espera de outra pessoa que as decida.</summary>
    [CodigoDeContrato("CONCESSAO_RETIDA_NA_MIGRACAO", "Concessão Retida Na Migração")]
    ConcessaoRetidaNaMigracao = 7,

    /// <summary>O perfil foi suspenso e nenhuma concessão dele vale até a retomada.</summary>
    [CodigoDeContrato("PERFIL_SUSPENSO", "Perfil Suspenso")]
    PerfilSuspenso = 8,

    /// <summary>A classificação de uma permissão de perfil subiu e a permissão aguarda liberação.</summary>
    [CodigoDeContrato("PERMISSAO_RECLASSIFICADA", "Permissão Reclassificada")]
    PermissaoReclassificada = 9,

    /// <summary>O escopo de uma permissão de perfil mudou e a permissão aguarda liberação.</summary>
    [CodigoDeContrato("ESCOPO_DA_PERMISSAO_ALTERADO", "Escopo Da Permissão Alterado")]
    EscopoDaPermissaoAlterado = 10,

    /// <summary>Um perfil de governança divergiu do que o código espera.</summary>
    [CodigoDeContrato("DIVERGENCIA_DA_GOVERNANCA", "Divergência Da Governança")]
    DivergenciaDaGovernanca = 11,

    /// <summary>Uma versão pendente ficou apta para a etapa de quem a aprova.</summary>
    [CodigoDeContrato("VERSAO_PENDENTE_APTA", "Versão Pendente Apta")]
    VersaoPendenteApta = 12,

    /// <summary>Migrações passaram a aguardar a decisão do Representante do controlador.</summary>
    [CodigoDeContrato("MIGRACOES_AGUARDANDO_PROTECAO_DE_DADOS", "Migrações Aguardando Proteção De Dados")]
    MigracoesAguardandoProtecaoDeDados = 13,

    /// <summary>As decisões da gestão de acessos foram enviadas com recusas.</summary>
    [CodigoDeContrato("DECISOES_ENVIADAS_COM_RECUSAS", "Decisões Enviadas Com Recusas")]
    DecisoesEnviadasComRecusas = 14,

    /// <summary>A concessão foi migrada para a versão nova do perfil.</summary>
    [CodigoDeContrato("CONCESSAO_MIGRADA", "Concessão Migrada")]
    ConcessaoMigrada = 15,

    /// <summary>A concessão foi revogada pela migração.</summary>
    [CodigoDeContrato("CONCESSAO_REVOGADA", "Concessão Revogada")]
    ConcessaoRevogada = 16,

    /// <summary>Concessões foram revogadas pela migração, para quem a gere.</summary>
    [CodigoDeContrato("REVOGACOES_NA_MIGRACAO", "Revogações Na Migração")]
    RevogacoesNaMigracao = 17,

    /// <summary>A versão anterior do perfil foi encerrada.</summary>
    [CodigoDeContrato("VERSAO_ENCERRADA", "Versão Encerrada")]
    VersaoEncerrada = 18,

    /// <summary>Quem tem o perfil perderá o acesso se a migração dele não for concluída até o prazo.</summary>
    [CodigoDeContrato("ADVERTENCIA_DE_FIM_DA_MIGRACAO", "Advertência De Fim Da Migração")]
    AdvertenciaDeFimDaMigracao = 19,

    /// <summary>O pedido em andamento foi devolvido porque o perfil pedido mudou.</summary>
    [CodigoDeContrato("PEDIDO_DEVOLVIDO_PELA_REVISAO", "Pedido Devolvido Pela Revisão")]
    PedidoDevolvidoPelaRevisao = 20,

    /// <summary>O pedido foi devolvido porque o perfil passou a exigir o termo de responsabilidade depois da criação.</summary>
    [CodigoDeContrato("PEDIDO_DEVOLVIDO_POR_TERMO_EXIGIDO", "Pedido Devolvido Por Termo Exigido")]
    PedidoDevolvidoPorTermoExigido = 21,

    /// <summary>Uma providência sobre concessão foi solicitada e aguarda resposta.</summary>
    [CodigoDeContrato("PROVIDENCIA_SOLICITADA", "Providência Solicitada")]
    ProvidenciaSolicitada = 22,

    /// <summary>A providência solicitada foi respondida.</summary>
    [CodigoDeContrato("PROVIDENCIA_RESPONDIDA", "Providência Respondida")]
    ProvidenciaRespondida = 23,

    /// <summary>A providência solicitada foi atendida, com a revogação da concessão.</summary>
    [CodigoDeContrato("PROVIDENCIA_ATENDIDA", "Providência Atendida")]
    ProvidenciaAtendida = 24,

    /// <summary>A concessão foi revogada pelo atendimento de uma providência, para o beneficiário.</summary>
    [CodigoDeContrato("CONCESSAO_REVOGADA_NO_ATENDIMENTO", "Concessão Revogada No Atendimento")]
    ConcessaoRevogadaNoAtendimento = 25,
}


/// <summary>Finalidade do aviso enviada ao serviço de email.</summary>
public static class FinalidadeDoAviso
{
    public static string Finalidade(this TipoDeAviso tipo) => $"perfil.{tipo.Codigo().ToLowerInvariant()}";
}
