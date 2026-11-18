namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Revisão das concessões quando uma nova versão do perfil acrescenta permissões. Dura 72 horas e mantém as
/// decisões, as situações das concessões e o encerramento. Uma concessão é elegível enquanto está ativa e não
/// pertence à versão de destino. A revisão aberta exige ao menos uma concessão pendente.
/// </summary>
public sealed class RevisaoDasConcessoesDoPerfil : EntityBase
{
    /// <summary>A duração da revisão, fixa.</summary>
    public static readonly TimeSpan Duracao = TimeSpan.FromHours(72);

    private readonly List<EnvioDeDecisoesDaRevisao> _envios = [];
    private readonly List<MigracaoDaConcessao> _migracoes = [];
    private readonly List<DecisaoDaMigracao> _decisoes = [];

    public Guid PerfilId { get; private set; }

    /// <summary>A versão que abriu a revisão.</summary>
    public Guid VersaoNovaId { get; private set; }

    public int NumeroDaVersaoNova { get; private set; }

    /// <summary>A versão para a qual as concessões migram: a nova, ou a reduzida que uma remoção na revisão pôs em vigor.</summary>
    public Guid VersaoAlvoId { get; private set; }

    public DateTimeOffset AbertaEm { get; private set; }

    /// <summary>
    /// A ordem em que as revisões foram abertas, dada pelo banco: a última revisão do perfil é a de maior abertura e, no
    /// mesmo instante, a de maior ordem de registro.
    /// </summary>
    public long OrdemDeRegistro { get; private set; }

    public DateTimeOffset TerminaEm { get; private set; }

    public DateTimeOffset? EncerradaEm { get; private set; }

    public FormaDoEncerramento? Forma { get; private set; }

    public string? MotivoDoEncerramento { get; private set; }

    public string? EncerradaPorEmissor { get; private set; }

    public string? EncerradaPorContaId { get; private set; }

    public string? EncerradaPorNome { get; private set; }

    public DateTimeOffset? Aviso48hEm { get; private set; }

    public DateTimeOffset? Aviso24hEm { get; private set; }

    public IReadOnlyList<EnvioDeDecisoesDaRevisao> Envios => _envios;

    public IReadOnlyList<MigracaoDaConcessao> Migracoes => _migracoes;

    public IReadOnlyList<DecisaoDaMigracao> Decisoes => _decisoes;

    public bool Encerrada => EncerradaEm is not null;

    /// <summary>O envio de decisões da gestão de acessos, que decide a lista inteira; nulo enquanto não foi enviado.</summary>
    public EnvioDeDecisoesDaRevisao? EnvioDaGestao => _envios.Find(static l => l.Etapa == EtapaDaMigracao.GestaoDeAcessos);

    // Construtor de materialização do EF Core.
    private RevisaoDasConcessoesDoPerfil()
    {
    }

    /// <summary>Abre a revisão para a versão que acaba de entrar em vigor.</summary>
    public static RevisaoDasConcessoesDoPerfil Abrir(Guid perfilId, VersaoDoPerfil nova, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(nova);

        return new RevisaoDasConcessoesDoPerfil
        {
            PerfilId = perfilId,
            VersaoNovaId = nova.Id,
            NumeroDaVersaoNova = nova.Numero,
            VersaoAlvoId = nova.Id,
            AbertaEm = agora,
            TerminaEm = agora + Duracao,
        };
    }

    /// <summary>Não encerrada, com o prazo por vencer e ao menos uma pendência.</summary>
    public bool EstaAberta(DateTimeOffset agora, int pendencias) => !Encerrada && agora < TerminaEm && pendencias > 0;

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

    /// <summary>Uma remoção na revisão põe em vigor uma versão reduzida, que passa a ser o destino das migrações.</summary>
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

    internal void Acrescentar(EnvioDeDecisoesDaRevisao envio) => _envios.Add(envio);

    internal void Acrescentar(DecisaoDaMigracao decisao) => _decisoes.Add(decisao);
    /// <summary>
    /// Quantidade que a pessoa pode decidir, ou nulo quando não há decisão disponível. Antes do envio da gestão,
    /// devolve zero se houver apenas concessões da própria pessoa, pois o envio completo as retém.
    /// </summary>
    public int? QuantidadeADecidir(EtapaDaMigracao etapa, Identidade quem, IReadOnlyList<Concessao> concessoes, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(quem);
        ArgumentNullException.ThrowIfNull(concessoes);

        if (!EstaAberta(agora, Pendencias(concessoes, agora)))
        {
            return null;
        }

        int quantidade = ContarDecisoes(etapa, quem, concessoes, agora);
        return quantidade > 0 || (etapa == EtapaDaMigracao.GestaoDeAcessos && EnvioDaGestao is null) ? quantidade : null;
    }

    private int ContarDecisoes(EtapaDaMigracao etapa, Identidade quem, IReadOnlyList<Concessao> concessoes, DateTimeOffset agora)
    {
        List<Concessao> deOutros = [.. concessoes.Where(c => Elegivel(c, agora) && c.Beneficiario != quem)];
        Dictionary<Guid, SituacaoDaMigracao> situacoes = _migracoes.ToDictionary(static m => m.ConcessaoId, static m => m.Situacao);
        return etapa == EtapaDaMigracao.ProtecaoDeDados
            ? DaProtecao(deOutros, situacoes, ConferenciasDaRevisao.ConcessoesDecididasNaGestaoPor(this, quem))
            : DaGestao(deOutros, situacoes);
    }

    private int DaGestao(List<Concessao> deOutros, Dictionary<Guid, SituacaoDaMigracao> situacoes) =>
        EnvioDaGestao is null ? deOutros.Count : deOutros.Count(c => situacoes.GetValueOrDefault(c.Id) == SituacaoDaMigracao.Retida);

    private static int DaProtecao(
        List<Concessao> deOutros, Dictionary<Guid, SituacaoDaMigracao> situacoes, HashSet<Guid> decididasPelaPessoa) =>
        deOutros.Count(c =>
            situacoes.GetValueOrDefault(c.Id) == SituacaoDaMigracao.AguardandoProtecaoDeDados && !decididasPelaPessoa.Contains(c.Id));

    /// <summary>
    /// O envio de decisões da gestão de acessos: decide cada elegível, menos as concessões do próprio gestor, que ficam retidas. O
    /// envio só vale completo e é atômico: toda recusa sai antes da primeira alteração.
    /// </summary>
    public Result EnviarDecisoesDaGestao(PedidoDoEnvio pedido, AutoriaDaVersao autoria, ContextoDaRevisao contexto)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(autoria);
        ArgumentNullException.ThrowIfNull(contexto);

        AutoriaDaVersao quem = ComConcessaoDoPerfil(autoria, contexto);
        if (ConferenciasDaRevisao.DoEnvioDaGestao(this, pedido, quem.Pessoa, contexto) is { } recusa)
        {
            return Result.Failure(recusa);
        }

        RegistrarDecisoesDaGestao(pedido, quem, contexto);
        EncerrarSeConcluida(contexto);

        return Result.Success();
    }

    /// <summary>A concessão retida, que outra pessoa decide depois do envio de decisões; o que o envio de decisões decidiu não muda.</summary>
    public Result DecidirRetida(PedidoDaRetida pedido, AutoriaDaVersao autoria, ContextoDaRevisao contexto)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(autoria);
        ArgumentNullException.ThrowIfNull(contexto);

        AutoriaDaVersao quem = ComConcessaoDoPerfil(autoria, contexto);
        if (ConferenciasDaRevisao.DaRetida(this, pedido, quem.Pessoa, contexto) is { } recusa)
        {
            return Result.Failure(recusa);
        }

        new AplicacaoDaDecisaoDaRevisao(this, contexto, quem).DaGestao(contexto.Concessao(pedido.ConcessaoId)!, pedido.Decisao!.Value, null);
        EncerrarSeConcluida(contexto);

        return Result.Success();
    }

    /// <summary>A etapa da proteção de dados, de uma concessão a várias; migra para a versão-alvo de agora ou revoga na hora.</summary>
    public Result DecidirEtapaDaProtecao(PedidoDaEtapaDaProtecao pedido, AutoriaDaVersao autoria, ContextoDaRevisao contexto)
    {
        ArgumentNullException.ThrowIfNull(pedido);
        ArgumentNullException.ThrowIfNull(autoria);
        ArgumentNullException.ThrowIfNull(contexto);

        AutoriaDaVersao quem = ComConcessaoDoPerfil(autoria, contexto);
        if (ConferenciasDaRevisao.DaEtapaDaProtecao(this, pedido, quem.Pessoa, contexto) is { } recusa)
        {
            return Result.Failure(recusa);
        }

        RegistrarDecisoesDaProtecao(pedido, quem, contexto);
        EncerrarSeConcluida(contexto);

        return Result.Success();
    }

    private void RegistrarDecisoesDaGestao(PedidoDoEnvio pedido, AutoriaDaVersao quem, ContextoDaRevisao contexto)
    {
        List<Concessao> elegiveis = [.. contexto.Concessoes.Where(c => Elegivel(c, contexto.Agora)).OrderBy(static c => c.Id)];
        EnvioDeDecisoesDaRevisao envio = EnvioDeDecisoesDaRevisao.Registrar(
            this, EtapaDaMigracao.GestaoDeAcessos, quem, pedido.ConfirmouExclusao, elegiveis.Count, contexto.Agora);
        Acrescentar(envio);
        AplicarDecisoesDaGestao(pedido, elegiveis, envio, contexto, quem);
    }

    private void RegistrarDecisoesDaProtecao(PedidoDaEtapaDaProtecao pedido, AutoriaDaVersao quem, ContextoDaRevisao contexto)
    {
        EnvioDeDecisoesDaRevisao envio = EnvioDeDecisoesDaRevisao.Registrar(
            this, EtapaDaMigracao.ProtecaoDeDados, quem, pedido.ConfirmouExclusao, pedido.Itens.Count, contexto.Agora);
        Acrescentar(envio);
        AplicacaoDaDecisaoDaRevisao efeitos = new(this, contexto, quem);
        pedido.Itens.ToList().ForEach(i => efeitos.DaProtecao(contexto.Concessao(i.ConcessaoId)!, i.Decisao!.Value, envio));
    }

    // A decisão da gestão, por elegível: as do próprio gestor ficam retidas; o item que já não é elegível é ignorado.
    private void AplicarDecisoesDaGestao(
        PedidoDoEnvio pedido, IReadOnlyList<Concessao> elegiveis, EnvioDeDecisoesDaRevisao envio, ContextoDaRevisao contexto, AutoriaDaVersao quem)
    {
        AplicacaoDaDecisaoDaRevisao efeitos = new(this, contexto, quem);
        Dictionary<Guid, ResultadoDaMigracao> decisoes = pedido.Itens.ToDictionary(static i => i.ConcessaoId, static i => i.Decisao!.Value);

        foreach (Concessao concessao in elegiveis)
        {
            if (decisoes.TryGetValue(concessao.Id, out ResultadoDaMigracao decisao))
            {
                efeitos.DaGestao(concessao, decisao, envio);
            }
            else
            {
                efeitos.Reter(concessao);
            }
        }
    }

    // Ter o perfil não impede o ato, mas a trilha registra: a conferência vale no instante da decisão.
    private static AutoriaDaVersao ComConcessaoDoPerfil(AutoriaDaVersao autoria, ContextoDaRevisao contexto) =>
        autoria with { TemConcessao = contexto.TemConcessaoDoPerfil(autoria.Pessoa) };

    /// <summary>
    /// O que toda operação confere antes de qualquer outra coisa: o prazo vencido materializa o fim da revisão, a
    /// concessão que saiu da lista é reconciliada e a revisão sem pendência se encerra. A recusa devolvida vem depois de a
    /// revisão ter mudado, e quem chama precisa gravá-la.
    /// </summary>
    public DomainError? ConferirEstado(ContextoDaRevisao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (contexto.Agora >= TerminaEm)
        {
            Materializar(contexto, FormaDoEncerramento.Prazo);
            return RevisaoDasConcessoesErros.PrazoEncerrado();
        }

        Reconciliar(contexto);

        return EncerrarSeConcluida(contexto) ? RevisaoDasConcessoesErros.RevisaoEncerrada() : null;
    }

    /// <summary>Encerra no menor instante entre o atual e o término, revogando as concessões ainda pendentes.</summary>
    public void Materializar(ContextoDaRevisao contexto, FormaDoEncerramento forma, EncerramentoManual? manual = null)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (Encerrada)
        {
            return;
        }

        DateTimeOffset encerradaEm = contexto.Agora < TerminaEm ? contexto.Agora : TerminaEm;
        new EncerramentoDaRevisao(this, contexto, encerradaEm).Encerrar();
        Encerrar(forma, encerradaEm, manual);
    }

    /// <summary>Leva a fora da lista a linha ainda aberta cuja concessão foi revogada ou venceu.</summary>
    public void Reconciliar(ContextoDaRevisao contexto)
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

    /// <summary>Encerra como concluída a revisão que não tem mais pendência; devolve se encerrou.</summary>
    public bool EncerrarSeConcluida(ContextoDaRevisao contexto)
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
    /// perde o acesso. A condição de a aprovação da versão nova estar concluída vale sempre, porque a revisão só abre nela.
    /// </summary>
    public Result EncerrarManualmente(EncerramentoManual encerramento, bool confirmouExclusao, ContextoDaRevisao contexto)
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
            return RevisaoDasConcessoesErros.MotivoInvalido(MotivoInformado.MaximoDeCaracteres);
        }

        return confirmouExclusao ? null : RevisaoDasConcessoesErros.ExclusaoNaoConfirmada();
    }

    internal void RegistrarDoSistema(
        Concessao? concessao, Guid concessaoId, SituacaoDaMigracao situacao, ContextoDaRevisao contexto, DateTimeOffset instante)
    {
        ComposicaoDoEnvio composicao = concessao is null
            ? new ComposicaoDoEnvio([], [], false)
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
