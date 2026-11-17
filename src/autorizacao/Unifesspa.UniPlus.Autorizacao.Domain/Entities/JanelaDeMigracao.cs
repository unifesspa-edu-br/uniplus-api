namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

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
public sealed class JanelaDeMigracao : EntityBase
{
    /// <summary>A duração da janela, fixa.</summary>
    public static readonly TimeSpan Duracao = TimeSpan.FromHours(72);

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

    /// <summary>
    /// A ordem em que as janelas foram abertas, dada pelo banco: a última janela do perfil é a de maior abertura e, no
    /// mesmo instante, a de maior ordem de registro.
    /// </summary>
    public long OrdemDeRegistro { get; private set; }

    public DateTimeOffset Prazo { get; private set; }

    public DateTimeOffset? EncerradaEm { get; private set; }

    public FormaDoEncerramento? Forma { get; private set; }

    public string? MotivoDoEncerramento { get; private set; }

    public string? EncerradaPorEmissor { get; private set; }

    public string? EncerradaPorContaId { get; private set; }

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

        return concessao.PerfilId == PerfilId && concessao.PendenteDeMigrarPara(VersaoAlvoId, agora);
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
        Guid concessaoId, SituacaoDaMigracao situacao, bool exigeProtecaoDeDados, DateTimeOffset instante)
    {
        if (LinhaDe(concessaoId) is { } linha)
        {
            linha.Mover(situacao, instante);
            return;
        }

        MigracaoDaConcessao nova = MigracaoDaConcessao.Criar(Id, PerfilId, concessaoId, situacao, exigeProtecaoDeDados, instante);
        nova.Mover(situacao, instante);
        _migracoes.Add(nova);
    }

    internal void Acrescentar(LoteDaMigracao lote) => _lotes.Add(lote);

    internal void Acrescentar(DecisaoDaMigracao decisao) => _decisoes.Add(decisao);

    /// <summary>
    /// O lote da gestão de acessos: decide cada elegível, menos as concessões do próprio gestor, que ficam retidas. O
    /// envio só vale completo e é atômico: toda recusa sai antes da primeira alteração.
    /// </summary>
    public Result EnviarLote(PedidoDoLote pedido, AutoriaDaVersao autoria, ContextoDaMigracao contexto)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(autoria);
        ArgumentNullException.ThrowIfNull(contexto);

        Result<AutoriaDaVersao> autor = ConferirPeloAutor(autoria, contexto, pessoa => ConferenciasDaMigracao.DoLote(this, pedido, pessoa, contexto));
        if (autor.IsFailure)
        {
            return Result.Failure(autor.Error!);
        }

        RegistrarOLote(pedido, autor.Value!, contexto);

        return Result.Success();
    }

    private void RegistrarOLote(PedidoDoLote pedido, AutoriaDaVersao quem, ContextoDaMigracao contexto)
    {
        List<Concessao> elegiveis = [.. contexto.Concessoes.Where(c => Elegivel(c, contexto.Agora)).OrderBy(static c => c.Id)];
        LoteDaMigracao lote = LoteDaMigracao.Registrar(
            this, EtapaDaMigracao.GestaoDeAcessos, quem, pedido.ConfirmouExclusao, elegiveis.Count, contexto.Agora);
        Acrescentar(lote);
        AplicarOLote(pedido, elegiveis, lote, new EfeitosDaDecisaoDaMigracao(this, contexto, quem));
        EncerrarSeConcluida(contexto);
    }

    /// <summary>A concessão retida, que outra pessoa decide depois do lote; o que o lote decidiu não muda.</summary>
    public Result DecidirRetida(PedidoDaRetida pedido, AutoriaDaVersao autoria, ContextoDaMigracao contexto)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(autoria);
        ArgumentNullException.ThrowIfNull(contexto);

        Result<AutoriaDaVersao> autor = ConferirPeloAutor(autoria, contexto, pessoa => ConferenciasDaMigracao.DaRetida(this, pedido, pessoa, contexto));
        if (autor.IsFailure)
        {
            return Result.Failure(autor.Error!);
        }

        new EfeitosDaDecisaoDaMigracao(this, contexto, autor.Value!).DaGestao(contexto.Concessao(pedido.ConcessaoId)!, pedido.Decisao!.Value, null);
        EncerrarSeConcluida(contexto);

        return Result.Success();
    }

    /// <summary>A etapa da proteção de dados, de uma concessão a várias; migra para a versão-alvo de agora ou revoga na hora.</summary>
    public Result DecidirEtapaDaProtecao(PedidoDaEtapaDaProtecao pedido, AutoriaDaVersao autoria, ContextoDaMigracao contexto)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(autoria);
        ArgumentNullException.ThrowIfNull(contexto);

        Result<AutoriaDaVersao> autor = ConferirPeloAutor(autoria, contexto, pessoa => ConferenciasDaMigracao.DaEtapaDaProtecao(this, pedido, pessoa, contexto));
        if (autor.IsFailure)
        {
            return Result.Failure(autor.Error!);
        }

        RegistrarEtapaDaProtecao(pedido, autor.Value!, contexto);

        return Result.Success();
    }

    private void RegistrarEtapaDaProtecao(PedidoDaEtapaDaProtecao pedido, AutoriaDaVersao quem, ContextoDaMigracao contexto)
    {
        LoteDaMigracao envio = LoteDaMigracao.Registrar(
            this, EtapaDaMigracao.ProtecaoDeDados, quem, pedido.ConfirmouExclusao, pedido.Itens.Count, contexto.Agora);
        Acrescentar(envio);
        EfeitosDaDecisaoDaMigracao efeitos = new(this, contexto, quem);
        pedido.Itens.ToList().ForEach(i => efeitos.DaProtecao(contexto.Concessao(i.ConcessaoId)!, i.Decisao!.Value, envio));
        EncerrarSeConcluida(contexto);
    }

    // A decisão da gestão, por elegível: as do próprio gestor ficam retidas; o item que já não é elegível é ignorado.
    private static void AplicarOLote(
        PedidoDoLote pedido, IReadOnlyList<Concessao> elegiveis, LoteDaMigracao lote, EfeitosDaDecisaoDaMigracao efeitos)
    {
        Dictionary<Guid, ResultadoDaMigracao> decisoes = pedido.Itens.ToDictionary(static i => i.ConcessaoId, static i => i.Decisao!.Value);

        foreach (Concessao concessao in elegiveis)
        {
            if (decisoes.TryGetValue(concessao.Id, out ResultadoDaMigracao decisao))
            {
                efeitos.DaGestao(concessao, decisao, lote);
            }
            else
            {
                efeitos.Reter(concessao);
            }
        }
    }

    // Toda recusa sai antes da primeira alteração: a conferência é feita pela pessoa, com a concessão do perfil no instante.
    private static Result<AutoriaDaVersao> ConferirPeloAutor(
        AutoriaDaVersao autoria, ContextoDaMigracao contexto, Func<Identidade, DomainError?> conferencia)
    {
        AutoriaDaVersao quem = ComConcessaoDoPerfil(autoria, contexto);
        DomainError? recusa = conferencia(quem.Pessoa);

        return recusa is null ? Result<AutoriaDaVersao>.Success(quem) : Result<AutoriaDaVersao>.Failure(recusa);
    }

    // Ter o perfil não impede o ato, mas a trilha registra: a conferência vale no instante da decisão.
    private static AutoriaDaVersao ComConcessaoDoPerfil(AutoriaDaVersao autoria, ContextoDaMigracao contexto) =>
        autoria with { TemConcessao = contexto.TemConcessaoDoPerfil(autoria.Pessoa) };

    /// <summary>
    /// O que toda operação confere antes de qualquer outra coisa: o prazo vencido materializa o fim da janela, a
    /// concessão que saiu da lista é reconciliada e a janela sem pendência se encerra. A recusa devolvida vem depois de a
    /// janela ter mudado, e quem chama precisa gravá-la.
    /// </summary>
    public DomainError? ConferirEstado(ContextoDaMigracao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (contexto.Agora >= Prazo)
        {
            Materializar(contexto, FormaDoEncerramento.Prazo);
            return MigracaoErros.PrazoEncerrado();
        }

        Reconciliar(contexto);

        return EncerrarSeConcluida(contexto) ? MigracaoErros.JanelaEncerrada() : null;
    }

    /// <summary>
    /// Materializa o fim da janela no corte, que é o menor entre o instante e o prazo: pendência no corte é quem não tinha
    /// revogação nem vencimento até ali e não tinha situação final. Ela é revogada pelo sistema, no corte, se ainda não
    /// foi; a linha cuja concessão saiu antes do corte fica fora da lista. Janela já encerrada não muda.
    /// </summary>
    public void Materializar(ContextoDaMigracao contexto, FormaDoEncerramento forma, EncerramentoManual? manual = null)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (Encerrada)
        {
            return;
        }

        DateTimeOffset corte = contexto.Agora < Prazo ? contexto.Agora : Prazo;
        new FimDaJanelaNoCorte(this, contexto, corte).Encerrar();
        Encerrar(forma, corte, manual);
    }

    /// <summary>Leva a fora da lista a linha ainda aberta cuja concessão foi revogada ou venceu.</summary>
    public void Reconciliar(ContextoDaMigracao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        foreach (MigracaoDaConcessao linha in _migracoes.Where(static m => !m.Situacao.EhFinal()).ToList())
        {
            Concessao? concessao = contexto.Concessao(linha.ConcessaoId);
            if (concessao is null || !concessao.EstaAtiva(contexto.Agora))
            {
                RegistrarDoSistema(concessao, linha.ConcessaoId, SituacaoDaMigracao.ForaDaLista, contexto, contexto.Agora);
            }
        }
    }

    /// <summary>Encerra como concluída a janela que não tem mais pendência; devolve se encerrou.</summary>
    public bool EncerrarSeConcluida(ContextoDaMigracao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (Encerrada || Pendencias(contexto.Concessoes, contexto.Agora) > 0)
        {
            return false;
        }

        Encerrar(FormaDoEncerramento.Concluida, contexto.Agora, null);

        return true;
    }

    /// <summary>
    /// A gestão de acessos encerra a versão anterior antes do prazo, com motivo e a confirmação de que quem não migrou
    /// perde o acesso. A condição de a aprovação da versão nova estar concluída vale sempre, porque a janela só abre nela.
    /// </summary>
    public Result EncerrarManualmente(EncerramentoManual encerramento, bool confirmouExclusao, ContextoDaMigracao contexto)
    {
        ArgumentNullException.ThrowIfNull(encerramento);
        ArgumentNullException.ThrowIfNull(contexto);

        MotivoInformado? motivo = MotivoInformado.Normalizar(encerramento.Motivo);
        if (RecusaDoEncerramento(motivo, confirmouExclusao) is { } recusa)
        {
            return Result.Failure(recusa);
        }

        Materializar(contexto, FormaDoEncerramento.Manual, encerramento with { Motivo = motivo?.Valor });

        return Result.Success();
    }

    private static DomainError? RecusaDoEncerramento(MotivoInformado? motivo, bool confirmouExclusao)
    {
        if (motivo is null)
        {
            return MigracaoErros.MotivoInvalido(MotivoInformado.MaximoDeCaracteres);
        }

        return confirmouExclusao ? null : MigracaoErros.ExclusaoNaoConfirmada();
    }

    internal void RegistrarDoSistema(
        Concessao? concessao, Guid concessaoId, SituacaoDaMigracao situacao, ContextoDaMigracao contexto, DateTimeOffset instante)
    {
        ComposicaoDoLote composicao = concessao is null
            ? new ComposicaoDoLote([], [], false)
            : contexto.ComposicaoDe(concessao);
        ResultadoDaMigracao resultado = situacao == SituacaoDaMigracao.ForaDaLista ? ResultadoDaMigracao.ForaDaLista : ResultadoDaMigracao.NaoMigrada;

        Registrar(concessaoId, situacao, composicao.ExigeProtecaoDeDados, instante);
        Acrescentar(DecisaoDaMigracao.DoSistema(this, concessaoId, resultado, composicao, instante));
    }

    private void Encerrar(FormaDoEncerramento forma, DateTimeOffset instante, EncerramentoManual? manual)
    {
        EncerradaEm = instante;
        Forma = forma;
        MotivoDoEncerramento = manual?.Motivo;
        EncerradaPorEmissor = manual?.Autoria.Pessoa.Emissor;
        EncerradaPorContaId = manual?.Autoria.Pessoa.ContaId;
        EncerradaPorNome = manual?.Autoria.Pessoa.Nome;
    }
}
