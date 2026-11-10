namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// A desativação do perfil: o perfil deixa de aceitar pedidos e versões novos, e as concessões continuam valendo. É o
/// registro de estado; a trilha é <see cref="EventoDaContencao"/>. Autoria, motivo e instante da desativação não se
/// alteram, e os campos da reativação passam de nulos a preenchidos uma única vez.
/// </summary>
public sealed class DesativacaoDoPerfil : EntityBase, IRegistroDaTrilhaDeContencao
{
    public Guid PerfilId { get; private init; }

    public DateTimeOffset DesativadaEm { get; private init; }

    public string DesativadaPorEmissor { get; private init; } = string.Empty;

    public string DesativadaPorSub { get; private init; } = string.Empty;

    public string? DesativadaPorNome { get; private init; }

    public string Motivo { get; private init; } = string.Empty;

    public DateTimeOffset? ReativadaEm { get; private set; }

    public string? ReativadaPorEmissor { get; private set; }

    public string? ReativadaPorSub { get; private set; }

    public string? ReativadaPorNome { get; private set; }

    public string? MotivoDaReativacao { get; private set; }

    public int OrdinalLocal { get; private init; }

    public long OrdemNaTrilha { get; private set; }

    public bool Aberta => ReativadaEm is null;

    public Identidade DesativadaPor => new(DesativadaPorEmissor, DesativadaPorSub, DesativadaPorNome);

    // Construtor de materialização do EF Core.
    private DesativacaoDoPerfil()
    {
    }

    internal static DesativacaoDoPerfil Abrir(Guid perfilId, Identidade autor, string motivo, DateTimeOffset instante, int ordinal) =>
        new()
        {
            PerfilId = perfilId,
            DesativadaEm = instante,
            DesativadaPorEmissor = autor.Emissor,
            DesativadaPorSub = autor.Sub,
            DesativadaPorNome = autor.Nome,
            Motivo = motivo,
            OrdinalLocal = ordinal,
        };

    internal void Reativar(Identidade autor, string motivo, DateTimeOffset instante)
    {
        ReativadaEm = instante;
        ReativadaPorEmissor = autor.Emissor;
        ReativadaPorSub = autor.Sub;
        ReativadaPorNome = autor.Nome;
        MotivoDaReativacao = motivo;
    }
}
