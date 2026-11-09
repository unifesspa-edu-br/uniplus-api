namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// A janela de migração de um perfil: abre quando entra em vigor uma versão que acrescenta permissão a um perfil com
/// concessão elegível e dura 72 horas corridas, sem prorrogação. Nela, cada concessão das versões anteriores é decidida
/// uma a uma, num lote completo; quem não migra perde o acesso. Um perfil tem no máximo uma janela não encerrada.
/// </summary>
/// <remarks>
/// <para>
/// <b>Concessão elegível</b> é a do perfil que não foi revogada, não venceu e ainda não está na versão-alvo. Pendência é
/// a elegível: a que já tem situação final saiu da elegibilidade (migrou, foi revogada ou venceu). A janela só bloqueia o
/// perfil enquanto está efetivamente aberta, o que exige ao menos uma pendência e o prazo por vencer; a que perdeu a
/// última pendência ou o prazo sem ter sido materializada não bloqueia nada, e a próxima operação a encerra.
/// </para>
/// <para>
/// <b>A janela é o agregado de todas as suas linhas.</b> Os lotes, a situação de cada concessão e as decisões só mudam
/// por ela: quem chama trava o perfil e as concessões e lhe entrega o contexto lido sob as travas.
/// </para>
/// </remarks>
public sealed partial class JanelaDeMigracao : EntityBase
{
    /// <summary>A duração da janela, fixa.</summary>
    public static readonly TimeSpan Duracao = TimeSpan.FromHours(72);

    /// <summary>O tamanho máximo do motivo do encerramento manual.</summary>
    public const int MotivoMaxLength = 500;

    private readonly List<LoteDaMigracao> _lotes = [];
    private readonly List<MigracaoDaConcessao> _migracoes = [];
    private readonly List<DecisaoDaMigracao> _decisoes = [];

    public Guid PerfilId { get; private set; }

    /// <summary>A versão que abriu a janela.</summary>
    public Guid VersaoNovaId { get; private set; }

    public int NumeroDaVersaoNova { get; private set; }

    /// <summary>A versão para a qual as concessões migram: a nova, ou a reduzida que uma remoção na janela pôs em vigor.</summary>
    public Guid VersaoAlvoId { get; private set; }

    public DateTimeOffset AbertaEm { get; private set; }

    public DateTimeOffset Prazo { get; private set; }

    public DateTimeOffset? EncerradaEm { get; private set; }

    public FormaDoEncerramento? Forma { get; private set; }

    public string? MotivoDoEncerramento { get; private set; }

    public string? EncerradaPorEmissor { get; private set; }

    public string? EncerradaPorSub { get; private set; }

    public string? EncerradaPorNome { get; private set; }

    public DateTimeOffset? Aviso48hEm { get; private set; }

    public DateTimeOffset? Aviso24hEm { get; private set; }

    public IReadOnlyList<LoteDaMigracao> Lotes => _lotes;

    public IReadOnlyList<MigracaoDaConcessao> Migracoes => _migracoes;

    public IReadOnlyList<DecisaoDaMigracao> Decisoes => _decisoes;

    public bool Encerrada => EncerradaEm is not null;

    /// <summary>O lote da gestão de acessos, que decide a lista inteira; nulo enquanto não foi enviado.</summary>
    public LoteDaMigracao? LoteDaGestao => _lotes.Find(static l => l.Etapa == EtapaDaMigracao.GestaoDeAcessos);

    // Construtor de materialização do EF Core.
    private JanelaDeMigracao()
    {
    }

    /// <summary>Abre a janela para a versão que acaba de entrar em vigor.</summary>
    public static JanelaDeMigracao Abrir(Guid perfilId, VersaoDoPerfil nova, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(nova);

        return new JanelaDeMigracao
        {
            PerfilId = perfilId,
            VersaoNovaId = nova.Id,
            NumeroDaVersaoNova = nova.Numero,
            VersaoAlvoId = nova.Id,
            AbertaEm = agora,
            Prazo = agora + Duracao,
        };
    }

    /// <summary>Não encerrada, com o prazo por vencer e ao menos uma pendência.</summary>
    public bool EstaAberta(DateTimeOffset agora, int pendencias) => !Encerrada && agora < Prazo && pendencias > 0;

    /// <summary>A concessão do perfil que não foi revogada, não venceu e ainda não está na versão-alvo.</summary>
    public bool Elegivel(Concessao concessao, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(concessao);

        return concessao.PerfilId == PerfilId && concessao.EstaAtiva(agora) && concessao.VersaoId != VersaoAlvoId;
    }

    public int Pendencias(IEnumerable<Concessao> concessoes, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(concessoes);

        return concessoes.Count(c => Elegivel(c, agora));
    }

    public MigracaoDaConcessao? LinhaDe(Guid concessaoId) => _migracoes.Find(m => m.ConcessaoId == concessaoId);

    /// <summary>Uma remoção na janela põe em vigor uma versão reduzida, que passa a ser o destino das migrações.</summary>
    public void RedirecionarAlvo(Guid versaoReduzidaId) => VersaoAlvoId = versaoReduzidaId;

    /// <summary>Marca o aviso de 48 horas como emitido.</summary>
    public void RegistrarAvisoDe48Horas(DateTimeOffset instante) => Aviso48hEm ??= instante;

    /// <summary>Marca o aviso de 24 horas como emitido; o de 48 horas que ficou para trás deixa de ser devido.</summary>
    public void RegistrarAvisoDe24Horas(DateTimeOffset instante)
    {
        Aviso48hEm ??= instante;
        Aviso24hEm ??= instante;
    }

    // A linha da concessão: criada na primeira situação e movida nas seguintes.
    internal void Registrar(
        Guid concessaoId, SituacaoDaMigracao situacao, bool exigeProtecaoDeDados, string? dispensa, DateTimeOffset instante)
    {
        if (LinhaDe(concessaoId) is { } linha)
        {
            linha.Mover(situacao, dispensa, instante);
            return;
        }

        MigracaoDaConcessao nova = MigracaoDaConcessao.Criar(Id, PerfilId, concessaoId, situacao, exigeProtecaoDeDados, instante);
        nova.Mover(situacao, dispensa, instante);
        _migracoes.Add(nova);
    }

    internal void Acrescentar(LoteDaMigracao lote) => _lotes.Add(lote);

    internal void Acrescentar(DecisaoDaMigracao decisao) => _decisoes.Add(decisao);
}
