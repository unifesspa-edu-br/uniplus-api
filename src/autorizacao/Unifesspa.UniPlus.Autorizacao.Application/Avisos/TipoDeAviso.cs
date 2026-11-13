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

    /// <summary>Abriu a janela de migração das concessões do perfil.</summary>
    JanelaDeMigracaoAberta = 4,

    /// <summary>Faltam 48 horas para o fim da janela de migração.</summary>
    JanelaDeMigracaoFaltam48Horas = 5,

    /// <summary>Faltam 24 horas para o fim da janela de migração.</summary>
    JanelaDeMigracaoFaltam24Horas = 6,

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

    /// <summary>O lote da gestão de acessos foi enviado com recusas.</summary>
    LoteEnviadoComRecusas = 14,

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
    PedidoDevolvidoPelaJanela = 20,

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
        [TipoDeAviso.PermissoesRemovidas] = "permissoes_removidas",
        [TipoDeAviso.VersaoCanceladaPelaRemocao] = "versao_cancelada_pela_remocao",
        [TipoDeAviso.AprovacoesDescartadas] = "aprovacoes_descartadas",
        [TipoDeAviso.JanelaDeMigracaoAberta] = "janela_de_migracao_aberta",
        [TipoDeAviso.JanelaDeMigracaoFaltam48Horas] = "janela_de_migracao_faltam_48_horas",
        [TipoDeAviso.JanelaDeMigracaoFaltam24Horas] = "janela_de_migracao_faltam_24_horas",
        [TipoDeAviso.ConcessaoRetidaNaMigracao] = "concessao_retida_na_migracao",
        [TipoDeAviso.PerfilSuspenso] = "perfil_suspenso",
        [TipoDeAviso.PermissaoReclassificada] = "permissao_reclassificada",
        [TipoDeAviso.EscopoDaPermissaoAlterado] = "escopo_da_permissao_alterado",
        [TipoDeAviso.DivergenciaDaGovernanca] = "divergencia_da_governanca",
        [TipoDeAviso.VersaoPendenteApta] = "versao_pendente_apta",
        [TipoDeAviso.MigracoesAguardandoProtecaoDeDados] = "migracoes_aguardando_protecao_de_dados",
        [TipoDeAviso.LoteEnviadoComRecusas] = "lote_enviado_com_recusas",
        [TipoDeAviso.ConcessaoMigrada] = "concessao_migrada",
        [TipoDeAviso.ConcessaoRevogada] = "concessao_revogada",
        [TipoDeAviso.RevogacoesNaMigracao] = "revogacoes_na_migracao",
        [TipoDeAviso.VersaoEncerrada] = "versao_encerrada",
        [TipoDeAviso.AdvertenciaDeFimDaMigracao] = "advertencia_de_fim_da_migracao",
        [TipoDeAviso.PedidoDevolvidoPelaJanela] = "pedido_devolvido_pela_janela",
        [TipoDeAviso.PedidoDevolvidoPorTermoExigido] = "pedido_devolvido_por_termo_exigido",
        [TipoDeAviso.ProvidenciaSolicitada] = "providencia_solicitada",
        [TipoDeAviso.ProvidenciaRespondida] = "providencia_respondida",
        [TipoDeAviso.ProvidenciaAtendida] = "providencia_atendida",
        [TipoDeAviso.ConcessaoRevogadaNoAtendimento] = "concessao_revogada_no_atendimento",
    }.ToFrozenDictionary();

    public static string ToCodigo(this TipoDeAviso tipo) =>
        Codigos.TryGetValue(tipo, out string? codigo) ? codigo : throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null);

    /// <summary>A finalidade que <c>EnviarEmail</c> leva aos logs: <c>perfil.{tipo}</c>.</summary>
    public static string Finalidade(this TipoDeAviso tipo) => $"perfil.{tipo.ToCodigo()}";
}
