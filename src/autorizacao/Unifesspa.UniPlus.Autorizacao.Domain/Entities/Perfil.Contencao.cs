namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A contenção do perfil: a suspensão, que bloqueia todas as concessões sem revogá-las, e a desativação, que só impede
/// pedidos e versões novos. Cada transição acrescenta o registro de estado e o evento da trilha na mesma gravação.
/// </summary>
public sealed partial class Perfil
{
    private readonly List<SuspensaoDoPerfil> _suspensoes = [];
    private readonly List<DesativacaoDoPerfil> _desativacoes = [];
    private readonly List<EventoDaContencao> _eventos = [];
    private int _posicoesDaTrilha;

    /// <summary>As suspensões carregadas com o perfil: a aberta, quando há, com as decisões da retomada.</summary>
    public IReadOnlyList<SuspensaoDoPerfil> Suspensoes => _suspensoes;

    /// <summary>As desativações carregadas com o perfil: a aberta, quando há.</summary>
    public IReadOnlyList<DesativacaoDoPerfil> Desativacoes => _desativacoes;

    /// <summary>Os eventos acrescentados na operação corrente. A trilha completa não se carrega com o perfil: vem do repositório.</summary>
    public IReadOnlyList<EventoDaContencao> Eventos => _eventos;

    public SuspensaoDoPerfil? SuspensaoAberta => _suspensoes.Find(static s => s.Aberta);

    public DesativacaoDoPerfil? DesativacaoAberta => _desativacoes.Find(static d => d.Aberta);

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

        DomainError? recusa = RecusaDeContencao(motivo) ?? (SuspensaoAberta is null ? null : PerfilErros.JaSuspenso());
        if (recusa is not null)
        {
            return Result.Failure(recusa);
        }

        AbrirSuspensao(autor, papel, MotivosDaVersao.Normalizar(motivo)!, instante, catalogo);

        return Result.Success();
    }

    /// <summary>Desativa o perfil. Desativar o que já está inativo não muda nada.</summary>
    public Result Desativar(Identidade autor, string? motivo, DateTimeOffset instante, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(autor);
        ArgumentNullException.ThrowIfNull(catalogo);

        DomainError? recusa = RecusaDeContencao(motivo);
        if (recusa is null && Ativo)
        {
            AbrirDesativacao(autor, MotivosDaVersao.Normalizar(motivo)!, instante, catalogo);
        }

        return recusa is null ? Result.Success() : Result.Failure(recusa);
    }

    /// <summary>Reativa o perfil, por outra pessoa que não quem o desativou. Reativar o que já está ativo não muda nada.</summary>
    public Result Reativar(Identidade autor, string? motivo, DateTimeOffset instante, ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(autor);
        ArgumentNullException.ThrowIfNull(catalogo);

        DomainError? recusa = RecusaDeContencao(motivo) ?? RecusaDaReativacao(autor);
        if (recusa is null && DesativacaoAberta is { } desativacao)
        {
            FecharDesativacao(desativacao, autor, MotivosDaVersao.Normalizar(motivo)!, instante, catalogo);
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
        DomainError? recusa = RecusaDeContencao(contexto.Motivo)
            ?? (SuspensaoAberta is { } suspensao ? suspensao.RecusaDaDecisao(contexto, protegido) : PerfilErros.NaoSuspenso());
        if (recusa is not null)
        {
            return Result.Failure(recusa);
        }

        DecisaoDaRetomada decisao = SuspensaoAberta!.Decidir(contexto, MotivosDaVersao.Normalizar(contexto.Motivo)!, protegido, ProximaPosicao());
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

    private DomainError? RecusaDeContencao(string? motivo) =>
        Governanca is not null ? PerfilErros.GovernancaImutavel()
        : MotivosDaVersao.Normalizar(motivo) is null ? PerfilErros.MotivoInvalido(MotivosDaVersao.MaxLength)
        : null;

    private DomainError? RecusaDaReativacao(Identidade autor) =>
        DesativacaoAberta is { } desativacao && desativacao.DesativadaPor.Equals(autor) ? PerfilErros.ReativacaoPorQuemDesativou() : null;

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
