namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

using System.Collections.Frozen;

/// <summary>Os avisos da governança de perfis que a aplicação emite.</summary>
public enum TipoDeAviso
{
    /// <summary>Permissões foram removidas de um perfil e a remoção já vale.</summary>
    PermissoesRemovidas = 1,

    /// <summary>A versão pendente foi cancelada pela contenção de uma remoção.</summary>
    VersaoCanceladaPelaRemocao = 2,

    /// <summary>As aprovações dadas a uma versão foram descartadas pela mudança de classe.</summary>
    AprovacoesDescartadas = 3,

    /// <summary>Abriu a revisão das concessões do perfil.</summary>
    RevisaoDasConcessoesAberta = 4,

    /// <summary>Faltam 48 horas para o fim da revisão das concessões.</summary>
    RevisaoDasConcessoesFaltam48Horas = 5,

    /// <summary>Faltam 24 horas para o fim da revisão das concessões.</summary>
    RevisaoDasConcessoesFaltam24Horas = 6,

    /// <summary>Concessões ficaram retidas, à espera de outra pessoa que as decida.</summary>
    ConcessaoRetidaNaMigracao = 7,

    /// <summary>O perfil foi suspenso e nenhuma concessão dele vale até a retomada.</summary>
    PerfilSuspenso = 8,

    /// <summary>A classificação de uma permissão de perfil subiu e a permissão aguarda liberação.</summary>
    PermissaoReclassificada = 9,

    /// <summary>O escopo de uma permissão de perfil mudou e a permissão aguarda liberação.</summary>
    EscopoDaPermissaoAlterado = 10,

    /// <summary>Um perfil de governança divergiu do que o código espera.</summary>
    DivergenciaDaGovernanca = 11,

    /// <summary>Uma versão pendente ficou apta para a etapa de quem a aprova.</summary>
    VersaoPendenteApta = 12,

    /// <summary>Migrações passaram a aguardar a decisão do Representante do controlador.</summary>
    MigracoesAguardandoProtecaoDeDados = 13,

    /// <summary>As decisões da gestão de acessos foram enviadas com recusas.</summary>
    DecisoesEnviadasComRecusas = 14,

    /// <summary>A concessão foi migrada para a versão nova do perfil.</summary>
    ConcessaoMigrada = 15,

    /// <summary>A concessão foi revogada pela migração.</summary>
    ConcessaoRevogada = 16,

    /// <summary>Concessões foram revogadas pela migração, para quem a gere.</summary>
    RevogacoesNaMigracao = 17,

    /// <summary>A versão anterior do perfil foi encerrada.</summary>
    VersaoEncerrada = 18,

    /// <summary>Quem tem o perfil perderá o acesso se a migração dele não for concluída até o prazo.</summary>
    AdvertenciaDeFimDaMigracao = 19,

    /// <summary>O pedido em andamento foi devolvido porque o perfil pedido mudou.</summary>
    PedidoDevolvidoPelaRevisao = 20,

    /// <summary>O pedido foi devolvido porque o perfil passou a exigir o termo de responsabilidade depois da criação.</summary>
    PedidoDevolvidoPorTermoExigido = 21,

    /// <summary>Uma providência sobre concessão foi solicitada e aguarda resposta.</summary>
    ProvidenciaSolicitada = 22,

    /// <summary>A providência solicitada foi respondida.</summary>
    ProvidenciaRespondida = 23,

    /// <summary>A providência solicitada foi atendida, com a revogação da concessão.</summary>
    ProvidenciaAtendida = 24,

    /// <summary>A concessão foi revogada pelo atendimento de uma providência, para o beneficiário.</summary>
    ConcessaoRevogadaNoAtendimento = 25,
}

/// <summary>O texto estável de cada tipo: a finalidade do envio e o que o registro de avisos guarda.</summary>
public static class TipoDeAvisoCodigo
{
    private static readonly FrozenDictionary<TipoDeAviso, string> Codigos = new Dictionary<TipoDeAviso, string>
    {
        [TipoDeAviso.PermissoesRemovidas] = "PERMISSOES_REMOVIDAS",
        [TipoDeAviso.VersaoCanceladaPelaRemocao] = "VERSAO_CANCELADA_PELA_REMOCAO",
        [TipoDeAviso.AprovacoesDescartadas] = "APROVACOES_DESCARTADAS",
        [TipoDeAviso.RevisaoDasConcessoesAberta] = "REVISAO_DAS_CONCESSOES_ABERTA",
        [TipoDeAviso.RevisaoDasConcessoesFaltam48Horas] = "REVISAO_DAS_CONCESSOES_FALTAM_48_HORAS",
        [TipoDeAviso.RevisaoDasConcessoesFaltam24Horas] = "REVISAO_DAS_CONCESSOES_FALTAM_24_HORAS",
        [TipoDeAviso.ConcessaoRetidaNaMigracao] = "CONCESSAO_RETIDA_NA_MIGRACAO",
        [TipoDeAviso.PerfilSuspenso] = "PERFIL_SUSPENSO",
        [TipoDeAviso.PermissaoReclassificada] = "PERMISSAO_RECLASSIFICADA",
        [TipoDeAviso.EscopoDaPermissaoAlterado] = "ESCOPO_DA_PERMISSAO_ALTERADO",
        [TipoDeAviso.DivergenciaDaGovernanca] = "DIVERGENCIA_DA_GOVERNANCA",
        [TipoDeAviso.VersaoPendenteApta] = "VERSAO_PENDENTE_APTA",
        [TipoDeAviso.MigracoesAguardandoProtecaoDeDados] = "MIGRACOES_AGUARDANDO_PROTECAO_DE_DADOS",
        [TipoDeAviso.DecisoesEnviadasComRecusas] = "DECISOES_ENVIADAS_COM_RECUSAS",
        [TipoDeAviso.ConcessaoMigrada] = "CONCESSAO_MIGRADA",
        [TipoDeAviso.ConcessaoRevogada] = "CONCESSAO_REVOGADA",
        [TipoDeAviso.RevogacoesNaMigracao] = "REVOGACOES_NA_MIGRACAO",
        [TipoDeAviso.VersaoEncerrada] = "VERSAO_ENCERRADA",
        [TipoDeAviso.AdvertenciaDeFimDaMigracao] = "ADVERTENCIA_DE_FIM_DA_MIGRACAO",
        [TipoDeAviso.PedidoDevolvidoPelaRevisao] = "PEDIDO_DEVOLVIDO_PELA_REVISAO",
        [TipoDeAviso.PedidoDevolvidoPorTermoExigido] = "PEDIDO_DEVOLVIDO_POR_TERMO_EXIGIDO",
        [TipoDeAviso.ProvidenciaSolicitada] = "PROVIDENCIA_SOLICITADA",
        [TipoDeAviso.ProvidenciaRespondida] = "PROVIDENCIA_RESPONDIDA",
        [TipoDeAviso.ProvidenciaAtendida] = "PROVIDENCIA_ATENDIDA",
        [TipoDeAviso.ConcessaoRevogadaNoAtendimento] = "CONCESSAO_REVOGADA_NO_ATENDIMENTO",
    }.ToFrozenDictionary();

    public static string ToCodigo(this TipoDeAviso tipo) =>
        Codigos.TryGetValue(tipo, out string? codigo) ? codigo : throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null);

    /// <summary>A finalidade que <c>EnviarEmail</c> leva aos logs: <c>perfil.{tipo}</c>.</summary>
    public static string Finalidade(this TipoDeAviso tipo) => $"perfil.{tipo.ToCodigo().ToLowerInvariant()}";
}
