namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A contenção do perfil inteiro: enquanto está aberta, nenhuma concessão do perfil vale na decisão de acesso, sem que
/// nenhuma seja revogada. É o registro de estado; a trilha é <see cref="EventoDaContencao"/>. Autoria, motivo e instante
/// não se alteram, e a retomada passa de nula a preenchida uma única vez.
/// </summary>
public sealed class SuspensaoDoPerfil : EntityBase, IRegistroDaTrilhaDeContencao
{
    private readonly List<DecisaoDaRetomada> _decisoes = [];

    public Guid PerfilId { get; private init; }

    public DateTimeOffset SuspensaEm { get; private init; }

    public string SuspensaPorEmissor { get; private init; } = string.Empty;

    public string SuspensaPorSub { get; private init; } = string.Empty;

    public string? SuspensaPorNome { get; private init; }

    /// <summary>A permissão que governou a suspensão: a gestão de acessos ou a proteção de dados.</summary>
    public EtapaDaVersao Papel { get; private init; }

    /// <summary>Até 500 caracteres; nunca vai nos avisos.</summary>
    public string Motivo { get; private init; } = string.Empty;

    public DateTimeOffset? RetomadaEm { get; private set; }

    public int OrdinalLocal { get; private init; }

    public long OrdemNaTrilha { get; private set; }

    public IReadOnlyList<DecisaoDaRetomada> Decisoes => _decisoes;

    public bool Aberta => RetomadaEm is null;

    public Identidade SuspensaPor => new(SuspensaPorEmissor, SuspensaPorSub, SuspensaPorNome);

    // Construtor de materialização do EF Core.
    private SuspensaoDoPerfil()
    {
    }

    /// <summary>A etapa da gestão de acessos é sempre exigida; a da proteção de dados, quando o perfil é protegido.</summary>
    public static IReadOnlyList<EtapaDaVersao> EtapasExigidas(bool perfilProtegido) =>
        perfilProtegido ? [EtapaDaVersao.GestaoDeAcessos, EtapaDaVersao.ProtecaoDeDados] : [EtapaDaVersao.GestaoDeAcessos];

    internal static SuspensaoDoPerfil Abrir(
        Guid perfilId, Identidade autor, EtapaDaVersao papel, string motivo, DateTimeOffset instante, int ordinal) =>
        new()
        {
            PerfilId = perfilId,
            SuspensaEm = instante,
            SuspensaPorEmissor = autor.Emissor,
            SuspensaPorSub = autor.Sub,
            SuspensaPorNome = autor.Nome,
            Papel = papel,
            Motivo = motivo,
            OrdinalLocal = ordinal,
        };

    /// <summary>As etapas exigidas agora que ainda não foram decididas.</summary>
    internal IReadOnlyList<EtapaDaVersao> Pendentes(bool perfilProtegido) =>
        [.. EtapasExigidas(perfilProtegido).Where(etapa => !_decisoes.Exists(d => d.Etapa == etapa))];

    /// <summary>Todas as etapas exigidas agora foram decididas.</summary>
    internal bool Completa(bool perfilProtegido) => Pendentes(perfilProtegido).Count == 0;

    internal void Retomar(DateTimeOffset instante) => RetomadaEm = instante;

    // A ordem das recusas é a que orienta: quem suspendeu, a etapa que não cabe, a etapa repetida e, por fim, a pessoa
    // que já decidiu a outra.
    internal DomainError? RecusaDaDecisao(ContextoDaRetomada contexto, bool perfilProtegido) =>
        SuspensaPor.Equals(contexto.Decisor) ? PerfilErros.RetomadaPorQuemSuspendeu()
        : contexto.Etapa == EtapaDaVersao.ProtecaoDeDados && !perfilProtegido ? PerfilErros.EtapaInvalida()
        : _decisoes.Exists(d => d.Etapa == contexto.Etapa) ? PerfilErros.EtapaDaRetomadaJaDecidida()
        : _decisoes.Exists(d => d.Decisor.Equals(contexto.Decisor)) ? PerfilErros.MesmoDecisorNasDuasEtapas()
        : null;

    internal DecisaoDaRetomada Decidir(ContextoDaRetomada contexto, string motivo, bool perfilProtegido, int ordinal)
    {
        DecisaoDaRetomada decisao = DecisaoDaRetomada.Registrar(Id, contexto, motivo, perfilProtegido, ordinal);
        _decisoes.Add(decisao);

        return decisao;
    }
}
