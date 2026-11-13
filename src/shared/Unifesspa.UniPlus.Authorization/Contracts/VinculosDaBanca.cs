namespace Unifesspa.UniPlus.Authorization.Contracts;

using Unifesspa.UniPlus.Authorization.Errors;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A banca a que um resource se refere e o lugar dela no processo: a fase em que ela atua e, quando o ato acontece
/// numa etapa, a etapa. É o que a verificação de designação confere; o processo e a inscrição continuam no
/// <see cref="ResourceContext"/>.
/// </summary>
public sealed record VinculosDaBanca
{
    /// <summary>Fase do cronograma em que a banca atua.</summary>
    public Guid FaseId { get; }

    /// <summary>Banca à qual a pessoa precisa estar designada.</summary>
    public Guid BancaId { get; }

    /// <summary>Etapa em que o ato acontece; nula quando o ato é da banca da fase, sem etapa.</summary>
    public Guid? EtapaId { get; }

    private VinculosDaBanca(Guid faseId, Guid bancaId, Guid? etapaId)
    {
        FaseId = faseId;
        BancaId = bancaId;
        EtapaId = etapaId;
    }

    /// <summary>Monta os vínculos; rejeita identificador vazio, que nunca aponta para uma banca real.</summary>
    public static Result<VinculosDaBanca> From(Guid faseId, Guid bancaId, Guid? etapaId = null) =>
        faseId == Guid.Empty || bancaId == Guid.Empty || etapaId == Guid.Empty
            ? Result<VinculosDaBanca>.Failure(new DomainError(
                AuthorizationErrorCodes.VinculosDaBancaInvalidos,
                "Fase, banca e etapa, quando informadas, precisam ser identificadores reais."))
            : Result<VinculosDaBanca>.Success(new VinculosDaBanca(faseId, bancaId, etapaId));
}
