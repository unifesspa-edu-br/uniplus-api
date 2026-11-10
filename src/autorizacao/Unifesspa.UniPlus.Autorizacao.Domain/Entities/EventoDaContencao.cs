namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// A trilha da contenção do perfil: suspensão, retomada, desativação e reativação. Só se acrescenta, nunca se altera nem
/// se carrega com o perfil: é evidência forense (ADR-0063). Cada evento guarda o que foi avaliado no instante, a versão
/// em vigor e se o perfil era protegido. A retomada é do sistema e leva a origem com a referência do caso; os demais
/// eventos são de uma pessoa.
/// </summary>
public sealed class EventoDaContencao : IForensicEntity, IRegistroDaTrilhaDeContencao
{
    public Guid Id { get; private set; } = Guid.CreateVersion7();

    public Guid PerfilId { get; private set; }

    public DateTimeOffset Instante { get; private set; }

    public TipoDoEventoDaContencao Tipo { get; private set; }

    public Guid? SuspensaoId { get; private set; }

    public Guid? DesativacaoId { get; private set; }

    public AtorDoEventoDaContencao Ator { get; private set; }

    public OrigemDaRetomada? Origem { get; private set; }

    public Guid? DecisaoId { get; private set; }

    public Guid? VersaoQueEntrouId { get; private set; }

    public string? PermissaoCodigo { get; private set; }

    public string? AtorEmissor { get; private set; }

    public string? AtorSub { get; private set; }

    public string? AtorNome { get; private set; }

    public EtapaDaVersao? Papel { get; private set; }

    public string? Motivo { get; private set; }

    public Guid? VersaoVigenteId { get; private set; }

    public bool PerfilProtegido { get; private set; }

    public int OrdinalLocal { get; private set; }

    public long OrdemNaTrilha { get; private set; }

    // Construtor de materialização do EF Core.
    private EventoDaContencao()
    {
    }

    private EventoDaContencao(Guid perfilId, TipoDoEventoDaContencao tipo, DateTimeOffset instante, RetratoDoPerfil retrato, int ordinal)
    {
        PerfilId = perfilId;
        Tipo = tipo;
        Instante = instante;
        VersaoVigenteId = retrato.VersaoVigenteId;
        PerfilProtegido = retrato.Protegido;
        OrdinalLocal = ordinal;
    }

    internal static EventoDaContencao DaSuspensao(SuspensaoDoPerfil suspensao, RetratoDoPerfil retrato, int ordinal) =>
        new EventoDaContencao(suspensao.PerfilId, TipoDoEventoDaContencao.Suspensao, suspensao.SuspensaEm, retrato, ordinal)
            .DeUmaPessoa(suspensao.SuspensaPor, suspensao.Motivo)
            .Referindo(suspensao.Id, null, suspensao.Papel);

    internal static EventoDaContencao DaDesativacao(DesativacaoDoPerfil desativacao, RetratoDoPerfil retrato, int ordinal) =>
        new EventoDaContencao(desativacao.PerfilId, TipoDoEventoDaContencao.Desativacao, desativacao.DesativadaEm, retrato, ordinal)
            .DeUmaPessoa(desativacao.DesativadaPor, desativacao.Motivo)
            .Referindo(null, desativacao.Id, null);

    internal static EventoDaContencao DaReativacao(
        DesativacaoDoPerfil desativacao, Identidade autor, string motivo, RetratoDoPerfil retrato, DateTimeOffset instante, int ordinal) =>
        new EventoDaContencao(desativacao.PerfilId, TipoDoEventoDaContencao.Reativacao, instante, retrato, ordinal)
            .DeUmaPessoa(autor, motivo)
            .Referindo(null, desativacao.Id, null);

    internal static EventoDaContencao DaRetomada(
        SuspensaoDoPerfil suspensao, OrigemDaReavaliacao origem, RetratoDoPerfil retrato, DateTimeOffset instante, int ordinal)
    {
        EventoDaContencao evento = new EventoDaContencao(suspensao.PerfilId, TipoDoEventoDaContencao.Retomada, instante, retrato, ordinal)
            .Referindo(suspensao.Id, null, null);
        evento.Ator = AtorDoEventoDaContencao.Sistema;
        evento.ReferirOrigem(origem);

        return evento;
    }

    private EventoDaContencao DeUmaPessoa(Identidade autor, string motivo)
    {
        Ator = AtorDoEventoDaContencao.Humano;
        AtorEmissor = autor.Emissor;
        AtorSub = autor.Sub;
        AtorNome = autor.Nome;
        Motivo = motivo;

        return this;
    }

    private EventoDaContencao Referindo(Guid? suspensaoId, Guid? desativacaoId, EtapaDaVersao? papel)
    {
        SuspensaoId = suspensaoId;
        DesativacaoId = desativacaoId;
        Papel = papel;

        return this;
    }

    private void ReferirOrigem(OrigemDaReavaliacao origem)
    {
        (Origem, DecisaoId, VersaoQueEntrouId, PermissaoCodigo) = origem switch
        {
            OrigemDaReavaliacao.PelaDecisao d => (OrigemDaRetomada.DecisaoDaEtapa, d.DecisaoId, (Guid?)null, (string?)null),
            OrigemDaReavaliacao.PelaTrocaDeVigencia t => (OrigemDaRetomada.TrocaDeVigencia, (Guid?)null, t.VersaoQueEntrouId, (string?)null),
            OrigemDaReavaliacao.PelaReclassificacao r => (OrigemDaRetomada.Reclassificacao, (Guid?)null, (Guid?)null, r.PermissaoCodigo),
            _ => throw new ArgumentOutOfRangeException(nameof(origem), origem, null),
        };
    }
}
