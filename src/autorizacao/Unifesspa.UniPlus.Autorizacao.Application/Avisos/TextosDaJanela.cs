namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

/// <summary>Os textos dos avisos da janela de migração, dos pedidos que ela devolve e da versão que ela encerra.</summary>
internal static class TextosDaJanela
{
    private const string OrientacaoParaPedirDeNovo = "Para voltar a ter o acesso, faça um novo pedido.";

    public static IEnumerable<KeyValuePair<TipoDeAviso, TextoDoAviso>> Todos { get; } =
    [
        TextoDoAviso.Para(
            TipoDeAviso.JanelaDeMigracaoAberta,
            "Janela de migração aberta para um perfil de acesso",
            "Uma versão que acrescenta permissão entrou em vigor. As concessões da versão anterior têm 72 horas para ser migradas; quem não migrar perde o acesso."),
        TextoDoAviso.Para(
            TipoDeAviso.JanelaDeMigracaoFaltam48Horas,
            "Faltam 48 horas para o fim da migração de um perfil de acesso",
            "Faltam 48 horas para o fim da migração. As concessões ainda pendentes serão revogadas no prazo e dependem da sua decisão."),
        TextoDoAviso.Para(
            TipoDeAviso.JanelaDeMigracaoFaltam24Horas,
            "Faltam 24 horas para o fim da migração de um perfil de acesso",
            "Faltam 24 horas para o fim da migração. As concessões ainda pendentes serão revogadas no prazo e dependem da sua decisão."),
        TextoDoAviso.Para(
            TipoDeAviso.ConcessaoRetidaNaMigracao,
            "Concessão retida na migração de um perfil de acesso",
            "Há concessões retidas na migração que só outra pessoa pode decidir, e o prazo corre."),
        TextoDoAviso.Para(
            TipoDeAviso.AdvertenciaDeFimDaMigracao,
            "Seu acesso a um perfil de acesso será perdido se a migração não for concluída",
            "Você perderá o acesso ao perfil nas horas indicadas se a migração da sua concessão não for concluída até o prazo."),
        TextoDoAviso.Para(
            TipoDeAviso.MigracoesAguardandoProtecaoDeDados,
            "Migrações de perfil de acesso aguardam o Representante do controlador",
            "A gestão de acessos migrou concessões cujo acréscimo exige a decisão do Representante do controlador. Sem ela até o prazo, as concessões perdem o acesso."),
        TextoDoAviso.Para(
            TipoDeAviso.LoteEnviadoComRecusas,
            "Lote de migração de perfil de acesso enviado com recusas",
            "O lote da gestão de acessos foi enviado com concessões recusadas, que já perderam o acesso."),
        TextoDoAviso.Para(
            TipoDeAviso.ConcessaoMigrada,
            "Seu acesso foi migrado para a nova versão do perfil",
            "A sua concessão foi migrada para a versão nova do perfil e passa a incluir as permissões acrescentadas."),
        TextoDoAviso.Para(
            TipoDeAviso.RevogacoesNaMigracao,
            "Concessões revogadas na migração de um perfil de acesso",
            "Concessões do perfil foram revogadas pela decisão de recusa na migração."),
        TextoDoAviso.Para(
            TipoDeAviso.PedidoDevolvidoPelaJanela,
            "Pedido de acesso devolvido",
            "O perfil de acesso pedido mudou e o pedido foi devolvido. Faça o pedido de novo depois que o perfil for atualizado."),
        TextoDoAviso.Para(
            TipoDeAviso.ConcessaoRevogada,
            "Seu acesso a um perfil de acesso foi revogado",
            "O seu acesso ao perfil foi revogado pela migração."),
        TextoDoAviso.Para(
            TipoDeAviso.VersaoEncerrada,
            "Versão anterior de perfil de acesso encerrada",
            "A versão anterior de um perfil de acesso foi encerrada."),
    ];

    /// <summary>A frase de abertura dos avisos cujo texto depende de como o fato aconteceu.</summary>
    public static IReadOnlyDictionary<(TipoDeAviso Tipo, FormaDoAviso Forma), string> PorForma { get; } =
        new Dictionary<(TipoDeAviso, FormaDoAviso), string>
        {
            [(TipoDeAviso.ConcessaoRevogada, FormaDoAviso.Recusa)] =
                $"A migração da sua concessão foi recusada, e o acesso ao perfil foi revogado. {OrientacaoParaPedirDeNovo}",
            [(TipoDeAviso.ConcessaoRevogada, FormaDoAviso.FimDoPrazo)] =
                $"O prazo da migração terminou sem que a sua concessão fosse migrada, e o acesso ao perfil foi revogado. {OrientacaoParaPedirDeNovo}",
            [(TipoDeAviso.ConcessaoRevogada, FormaDoAviso.EncerramentoManual)] =
                $"A gestão de acessos encerrou a migração antes do prazo, e o acesso ao perfil foi revogado. {OrientacaoParaPedirDeNovo}",
            [(TipoDeAviso.VersaoEncerrada, FormaDoAviso.VersaoSubstituida)] =
                "A versão anterior do perfil foi substituída por uma versão nova que não exige migração.",
            [(TipoDeAviso.VersaoEncerrada, FormaDoAviso.SemConcessaoAMigrar)] =
                "A versão anterior do perfil foi substituída por uma versão nova sem nenhuma concessão a migrar.",
            [(TipoDeAviso.VersaoEncerrada, FormaDoAviso.JanelaConcluida)] =
                "A migração do perfil terminou porque não restou concessão pendente, e a versão anterior foi encerrada.",
            [(TipoDeAviso.VersaoEncerrada, FormaDoAviso.FimDoPrazo)] =
                "O prazo da migração do perfil terminou, e a versão anterior foi encerrada.",
            [(TipoDeAviso.VersaoEncerrada, FormaDoAviso.EncerramentoManual)] =
                "A gestão de acessos encerrou a migração do perfil antes do prazo, e a versão anterior foi encerrada.",
        };
}
