namespace Unifesspa.UniPlus.Selecao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// A inscrição que tem linha válida num lote, com o número de inscrição que o INEP lhe deu. O
/// número é atributo da linha do INEP, não da nota de uma área, e fica guardado uma vez para a
/// conferência de um recurso.
/// </summary>
public sealed class InscritoDoLoteDeNotas : IForensicEntity
{
    public const int DigitosDoNumeroDeInscricao = 12;

    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid LoteId { get; private init; }

    public Guid InscricaoId { get; private init; }

    public string NumeroDeInscricaoNoEnem { get; private init; } = null!;

    // Construtor de materialização do EF Core.
    private InscritoDoLoteDeNotas()
    {
    }

    public static InscritoDoLoteDeNotas Registrar(Guid loteId, Guid inscricaoId, string numeroDeInscricaoNoEnem)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(loteId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(inscricaoId, Guid.Empty);
        ArgumentNullException.ThrowIfNull(numeroDeInscricaoNoEnem);

        if (numeroDeInscricaoNoEnem.Length != DigitosDoNumeroDeInscricao
            || numeroDeInscricaoNoEnem.Any(static c => c is < '0' or > '9'))
        {
            throw new ArgumentException("O número de inscrição no ENEM deve ter doze dígitos.", nameof(numeroDeInscricaoNoEnem));
        }

        return new InscritoDoLoteDeNotas
        {
            LoteId = loteId,
            InscricaoId = inscricaoId,
            NumeroDeInscricaoNoEnem = numeroDeInscricaoNoEnem,
        };
    }
}
