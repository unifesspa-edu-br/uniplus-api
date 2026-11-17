namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Perfil de acesso: um nome e um conjunto de permissões do catálogo que a concessão atribui de uma vez a quem exerce
/// uma função.
/// </summary>
/// <remarks>
/// <para>
/// <b>O perfil comum é versionado.</b> Nome, descrição e permissões vêm da versão vigente, e uma versão proposta só
/// entra em vigor com a aprovação da gestão de acessos e, quando o acréscimo é protegido, da proteção de dados; a
/// remoção de permissões vale na hora. Versão não se edita: mudar o perfil é criar outra versão.
/// <see cref="Nome"/> e <see cref="Descricao"/> são a identificação corrente: os da versão vigente ou, enquanto não há
/// vigente, os da última pendente.
/// </para>
/// <para>
/// <b>A sensibilidade do perfil não é gravada.</b> Ela é a mais alta entre as permissões em vigor e se deriva do
/// catálogo a cada leitura.
/// </para>
/// <para>
/// <b>Perfil não se exclui, se desativa.</b> Desativar não revoga concessão alguma nem cancela versão; só impede
/// pedidos e versões novos. A contenção do perfil inteiro é a suspensão, que bloqueia todas as concessões na decisão de
/// acesso sem revogá-las.
/// </para>
/// <para>
/// <b>Perfis de governança</b> (<see cref="Governanca"/> preenchida) são de sistema: nascem da carga inicial, têm as
/// permissões próprias, sem versão, são os únicos que podem reunir permissões reservadas à governança e não se
/// alteram, suspendem, reativam nem desativam por esta API.
/// </para>
/// </remarks>
public sealed class Perfil : EntityBase
{
    private readonly List<PermissaoDoPerfil> _permissoes = [];
    private readonly List<VersaoDoPerfil> _versoes = [];
    private readonly List<VigenciaDoPerfil> _vigencias = [];
    private readonly List<SuspensaoDoPerfil> _suspensoes = [];
    private readonly List<DesativacaoDoPerfil> _desativacoes = [];
    private readonly List<EventoDaContencao> _eventos = [];
    private int _posicoesDaTrilha;

    public string Nome { get; private set; } = string.Empty;

    public string Descricao { get; private set; } = string.Empty;

    public bool Ativo { get; private set; } = true;

    /// <summary>Papel de governança do perfil de sistema; nulo nos perfis comuns.</summary>
    public TipoDeGovernanca? Governanca { get; private set; }

    /// <summary>
    /// Os perfis de gestor de acessos e de Representante do controlador não admitem concessão com prazo: a concessão que
    /// vencesse sozinha furaria o mínimo de dois gestores ou deixaria a instituição sem Representante vigente, e só a
    /// revogação confere esses mínimos. O Encarregado de dados pode ser designado por prazo, e a concessão vencida deixa de
    /// valer para toda regra.
    /// </summary>
    public bool AdmitePrazo => AdmitePrazoCom(Governanca);

    /// <summary>Permissões próprias do perfil de governança; o perfil comum as tem na versão.</summary>
    public IReadOnlyCollection<PermissaoDoPerfil> Permissoes => _permissoes;

    public IReadOnlyList<VersaoDoPerfil> Versoes => _versoes;

    public IReadOnlyList<VigenciaDoPerfil> Vigencias => _vigencias;

    /// <summary>As suspensões carregadas com o perfil: a aberta, quando há, com as decisões da retomada.</summary>
    public IReadOnlyList<SuspensaoDoPerfil> Suspensoes => _suspensoes;

    /// <summary>As desativações carregadas com o perfil: a aberta, quando há.</summary>
    public IReadOnlyList<DesativacaoDoPerfil> Desativacoes => _desativacoes;

    /// <summary>Os eventos acrescentados na operação corrente. A trilha completa não se carrega com o perfil: vem do repositório.</summary>
    public IReadOnlyList<EventoDaContencao> Eventos => _eventos;

    public SuspensaoDoPerfil? SuspensaoAberta => _suspensoes.Find(static s => s.Aberta);

    public DesativacaoDoPerfil? DesativacaoAberta => _desativacoes.Find(static d => d.Aberta);

    public VersaoDoPerfil? VersaoVigente => _versoes.Find(static v => v.Situacao == SituacaoDaVersao.Vigente);

    public VersaoDoPerfil? VersaoPendente => _versoes.Find(static v => v.Pendente);

    /// <summary>A versão mais recente do perfil, quando terminou sem entrar em vigor: recusada, cancelada ou expirada.</summary>
    public VersaoDoPerfil? UltimaVersaoEncerradaSemVigor =>
        _versoes.MaxBy(static v => v.Numero) is { Situacao: SituacaoDaVersao.Recusada or SituacaoDaVersao.Cancelada or SituacaoDaVersao.Expirada } sem
            ? sem
            : null;

    /// <summary>
    /// A versão cujas permissões o perfil mostra: a vigente ou, enquanto o perfil ainda não tem vigente, a pendente.
    /// </summary>
    public VersaoDoPerfil? VersaoParaExibir => VersaoVigente ?? VersaoPendente;

    /// <summary>O perfil de governança dispensa a versão; o comum só vale com uma vigente.</summary>
    public bool TemComposicaoEmVigor => Governanca is not null || VersaoVigente is not null;

    /// <summary>
    /// Códigos das permissões em vigor, ordenados: as da versão vigente no perfil comum, as próprias no perfil de
    /// governança, nenhuma no perfil comum sem vigente. É a única leitura das permissões que decide acesso.
    /// </summary>
    public IReadOnlyList<string> ComposicaoEmVigor =>
        Governanca is not null
            ? [.. _permissoes.Select(static p => p.Codigo).Order(StringComparer.Ordinal)]
            : VersaoVigente?.Codigos() ?? [];

    /// <summary>A regra de <see cref="AdmitePrazo"/> a partir do papel de governança, para o retrato avaliado do perfil.</summary>
    public static bool AdmitePrazoCom(TipoDeGovernanca? governanca) =>
        governanca is not (TipoDeGovernanca.GestorDeAcessos or TipoDeGovernanca.RepresentanteDoControlador);

    // Construtor de materialização do EF Core.
    private Perfil()
    {
    }

    /// <summary>
    /// Cria um perfil comum, ativo e sem versão vigente, com a versão 1 pendente e o documento que a institui esperando
    /// o envio. A unicidade do nome é do handler, com proteção de corrida no índice único.
    /// </summary>
    public static Result<Perfil> Criar(
        ConteudoDaVersao conteudo, AutoriaDaVersao autoria, DateTimeOffset instante, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(conteudo);
        ArgumentNullException.ThrowIfNull(autoria);
        ArgumentNullException.ThrowIfNull(catalogo);

        Perfil perfil = new();
        Result<VersaoDoPerfil> versao = VersaoDoPerfil.Criar(perfil.Id, 1, conteudo, autoria, ProposicaoDaVersao.ClasseDe(conteudo, null, catalogo), instante);
        if (versao.IsFailure)
        {
            return Result<Perfil>.ValidationFailure(versao.Errors);
        }

        perfil.Acrescentar(versao.Value!);

        return Result<Perfil>.Success(perfil);
    }

    /// <summary>
    /// O alcance em que o perfil se concede: unidade quando todas as permissões em vigor são de unidade, instituição
    /// quando nenhuma é. Nulo quando mistura os dois.
    /// </summary>
    public TipoDeEscopo? Alcance(ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        return AlcanceDasPermissoes.De(ComposicaoEmVigor, catalogo);
    }

    internal Result<VersaoDoPerfil> ObterVersao(Guid versaoId) =>
        _versoes.Find(v => v.Id == versaoId) is { } versao
            ? Result<VersaoDoPerfil>.Success(versao)
            : Result<VersaoDoPerfil>.Failure(PerfilErros.VersaoNaoEncontrada());

    internal int ProximoNumero() => _versoes.Max(static v => v.Numero) + 1;

    // Sem versão vigente o perfil se identifica pela última pendente.
    internal void Acrescentar(VersaoDoPerfil versao)
    {
        _versoes.Add(versao);

        if (VersaoVigente is null)
        {
            Nome = versao.Nome;
            Descricao = versao.Descricao;
        }
    }

    /// <summary>
    /// Decide uma etapa da aprovação da versão: aprova ou recusa. O prazo vencido e a mudança de classe gravam mesmo
    /// recusando; a decisão que completa a aprovação troca a versão em vigor, e o resultado traz a troca.
    /// </summary>
    public DesfechoDaVersao Decidir(
        PedidoDeDecisaoDaVersao pedida, ContextoDaVersao contexto, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(pedida);
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(catalogo);

        Result<VersaoDoPerfil> versao = ObterVersaoDeOperacao(pedida.VersaoId);

        return versao.IsFailure
            ? DesfechoDaVersao.Recusa(versao.Error!)
            : new PassosDaDecisaoDaVersao(this, VersaoVigente, versao.Value!, pedida, contexto, catalogo).Executar();
    }

    /// <summary>
    /// Cancela a versão pendente, com o motivo, a pedido de quem a criou ou de quem mantém perfis. A versão vigente só
    /// deixa de valer quando outra a substitui. A pendente vencida expira em vez de cancelar.
    /// </summary>
    public DesfechoDaVersao CancelarVersao(Guid versaoId, Identidade autor, string? motivo, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(autor);

        Result<VersaoDoPerfil> versao = ObterVersaoDeOperacao(versaoId);
        DesfechoDaVersao? recusa = versao.IsFailure
            ? DesfechoDaVersao.Recusa(versao.Error!)
            : RecusaAoCancelar(versao.Value!, motivo, instante);
        if (recusa is not null)
        {
            return recusa;
        }

        versao.Value!.Cancelar(autor, MotivoInformado.Normalizar(motivo)?.Valor, FormaDoCancelamento.Pessoa, instante);

        return DesfechoDaVersao.Concluido();
    }

    /// <summary>
    /// Materializa o fim do prazo da pendente, para que a trilha não dependa de a tarefa agendada ter rodado. Devolve
    /// verdadeiro quando o perfil mudou e precisa ser gravado.
    /// </summary>
    public bool MaterializarExpiracao(DateTimeOffset agora)
    {
        if (VersaoPendente is not { } pendente || !pendente.Expirou(agora))
        {
            return false;
        }

        pendente.Expirar();

        return true;
    }

    // A vigência aberta se encerra antes de a nova abrir: o banco admite uma só aberta por perfil. Com a suspensão
    // aberta, a troca reavalia a retomada, porque a composição em vigor pode ter mudado de classe.
    internal TrocaDeVigencia TrocarVigencia(VersaoDoPerfil nova, DateTimeOffset instante, ICatalogoDePermissoesConsultavel catalogo)
    {
        VersaoDoPerfil? anterior = VersaoVigente;
        _vigencias.Find(static v => v.Aberta)?.Encerrar(instante);
        anterior?.Encerrar();

        nova.EntrarEmVigor();
        _vigencias.Add(VigenciaDoPerfil.Abrir(Id, nova.Id, instante));
        Nome = nova.Nome;
        Descricao = nova.Descricao;
        ReavaliarRetomada(new OrigemDaReavaliacao.PelaTrocaDeVigencia(nova.Id), catalogo, instante);

        return new TrocaDeVigencia(anterior, nova);
    }

    // O perfil de governança não tem versão e não se altera por esta API: a recusa é a da imutabilidade.
    private Result<VersaoDoPerfil> ObterVersaoDeOperacao(Guid versaoId) =>
        Governanca is not null
            ? Result<VersaoDoPerfil>.Failure(PerfilErros.GovernancaImutavel())
            : ObterVersao(versaoId);

    private DesfechoDaVersao? RecusaAoCancelar(VersaoDoPerfil versao, string? motivo, DateTimeOffset instante)
    {
        if (MaterializarExpiracao(instante))
        {
            return DesfechoDaVersao.RecusaGravada(PerfilErros.VersaoExpirada());
        }

        DomainError? recusa = ConferenciasDaVersao.DoCancelamento(versao, motivo);

        return recusa is null ? null : DesfechoDaVersao.Recusa(recusa);
    }

    /// <summary>A composição em vigor tem permissão pessoal, identificadora ou sensível, pelo catálogo atual.</summary>
    public bool Protegido(ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(catalogo);

        return ComposicaoEmVigor.Any(c => catalogo.Sensibilidade(c) >= ClassificacaoDeProtecao.Pessoal);
    }

    /// <summary>Suspende o perfil: nenhuma concessão dele vale até a retomada. Suspender o perfil suspenso é recusado.</summary>
    public Result Suspender(
        Identidade autor, EtapaDaVersao papel, string? motivo, DateTimeOffset instante, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(autor);
        ArgumentNullException.ThrowIfNull(catalogo);

        DomainError? recusa = ConferenciasDaContencao.DaContencao(this, MotivoInformado.Normalizar(motivo)) ?? (SuspensaoAberta is null ? null : PerfilErros.JaSuspenso());
        if (recusa is not null)
        {
            return Result.Failure(recusa);
        }

        AbrirSuspensao(autor, papel, MotivoInformado.Normalizar(motivo)!.Valor, instante, catalogo);

        return Result.Success();
    }

    /// <summary>Desativa o perfil. Desativar o que já está inativo não muda nada.</summary>
    public Result Desativar(Identidade autor, string? motivo, DateTimeOffset instante, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(autor);
        ArgumentNullException.ThrowIfNull(catalogo);

        DomainError? recusa = ConferenciasDaContencao.DaContencao(this, MotivoInformado.Normalizar(motivo));
        if (recusa is null && Ativo)
        {
            AbrirDesativacao(autor, MotivoInformado.Normalizar(motivo)!.Valor, instante, catalogo);
        }

        return recusa is null ? Result.Success() : Result.Failure(recusa);
    }

    /// <summary>Reativa o perfil, por outra pessoa que não quem o desativou. Reativar o que já está ativo não muda nada.</summary>
    public Result Reativar(Identidade autor, string? motivo, DateTimeOffset instante, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(autor);
        ArgumentNullException.ThrowIfNull(catalogo);

        DomainError? recusa = ConferenciasDaContencao.DaContencao(this, MotivoInformado.Normalizar(motivo)) ?? ConferenciasDaContencao.DaReativacao(this, autor);
        if (recusa is null && DesativacaoAberta is { } desativacao)
        {
            FecharDesativacao(desativacao, autor, MotivoInformado.Normalizar(motivo)!.Valor, instante, catalogo);
        }

        return recusa is null ? Result.Success() : Result.Failure(recusa);
    }

    /// <summary>
    /// Decide uma etapa da retomada. Quando as etapas exigidas agora estão decididas, a suspensão se fecha pela própria
    /// decisão.
    /// </summary>
    public Result DecidirRetomada(ContextoDaRetomada contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        bool protegido = Protegido(contexto.Catalogo);
        DomainError? recusa = ConferenciasDaContencao.DaContencao(this, contexto.Motivo)
            ?? (SuspensaoAberta is { } suspensao ? suspensao.RecusaDaDecisao(contexto, protegido) : PerfilErros.NaoSuspenso());
        if (recusa is not null)
        {
            return Result.Failure(recusa);
        }

        DecisaoDaRetomada decisao = SuspensaoAberta!.Decidir(contexto, contexto.Motivo!.Valor, protegido, ProximaPosicao());
        ReavaliarRetomada(new OrigemDaReavaliacao.PelaDecisao(decisao.Id), contexto.Catalogo, contexto.Instante);

        return Result.Success();
    }

    /// <summary>
    /// Fecha a suspensão quando as etapas exigidas naquele instante estão decididas: a gestão de acessos decidiu e, se o
    /// perfil é protegido, a proteção de dados também. Sem suspensão aberta não faz nada. Devolve se fechou.
    /// </summary>
    public bool ReavaliarRetomada(OrigemDaReavaliacao origem, ICatalogoDePermissoesConsultavel catalogo, DateTimeOffset instante)
    {
        ArgumentNullException.ThrowIfNull(origem);
        ArgumentNullException.ThrowIfNull(catalogo);

        if (SuspensaoAberta is not { } suspensao || !suspensao.Completa(Protegido(catalogo)))
        {
            return false;
        }

        suspensao.Retomar(instante);
        _eventos.Add(EventoDaContencao.DaRetomada(suspensao, origem, Retrato(catalogo), instante, ProximaPosicao()));

        return true;
    }

    /// <summary>As etapas exigidas agora que a suspensão aberta ainda não decidiu; vazio sem suspensão.</summary>
    public IReadOnlyList<EtapaDaVersao> EtapasPendentesDaRetomada(ICatalogoDePermissoesConsultavel catalogo) =>
        SuspensaoAberta is { } suspensao ? suspensao.Pendentes(Protegido(catalogo)) : [];

    private void AbrirSuspensao(
        Identidade autor, EtapaDaVersao papel, string motivo, DateTimeOffset instante, ICatalogoDePermissoesConsultavel catalogo)
    {
        SuspensaoDoPerfil suspensao = SuspensaoDoPerfil.Abrir(Id, autor, papel, motivo, instante, ProximaPosicao());
        _suspensoes.Add(suspensao);
        _eventos.Add(EventoDaContencao.DaSuspensao(suspensao, Retrato(catalogo), ProximaPosicao()));
    }

    private void AbrirDesativacao(Identidade autor, string motivo, DateTimeOffset instante, ICatalogoDePermissoesConsultavel catalogo)
    {
        DesativacaoDoPerfil desativacao = DesativacaoDoPerfil.Abrir(Id, autor, motivo, instante, ProximaPosicao());
        _desativacoes.Add(desativacao);
        _eventos.Add(EventoDaContencao.DaDesativacao(desativacao, Retrato(catalogo), ProximaPosicao()));
        Ativo = false;
    }

    private void FecharDesativacao(
        DesativacaoDoPerfil desativacao, Identidade autor, string motivo, DateTimeOffset instante, ICatalogoDePermissoesConsultavel catalogo)
    {
        desativacao.Reativar(autor, motivo, instante);
        _eventos.Add(EventoDaContencao.DaReativacao(desativacao, autor, motivo, Retrato(catalogo), instante, ProximaPosicao()));
        Ativo = true;
    }

    private RetratoDoPerfil Retrato(ICatalogoDePermissoesConsultavel catalogo) => new(VersaoVigente?.Id, Protegido(catalogo));

    private int ProximaPosicao() => ++_posicoesDaTrilha;
}
