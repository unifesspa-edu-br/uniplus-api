namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Intervalo em que uma versão foi a composição em vigor do perfil. Só a vigência aberta (<see cref="Ate"/> nulo) vale
/// agora, e há no máximo uma por perfil.
/// </summary>
public sealed class VigenciaDoPerfil : EntityBase
{
    public Guid PerfilId { get; private set; }

    public Guid VersaoId { get; private set; }

    public DateTimeOffset Desde { get; private set; }

    public DateTimeOffset? Ate { get; private set; }

    public bool Aberta => Ate is null;

    // Construtor de materialização do EF Core.
    private VigenciaDoPerfil()
    {
    }

    internal static VigenciaDoPerfil Abrir(Guid perfilId, Guid versaoId, DateTimeOffset desde) =>
        new() { PerfilId = perfilId, VersaoId = versaoId, Desde = desde };

    internal void Encerrar(DateTimeOffset instante) => Ate = instante;
}
