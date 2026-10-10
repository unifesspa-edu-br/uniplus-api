namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

/// <summary>O aceite de um termo na inscrição, com a versão e o hash da versão fixada.</summary>
public sealed class AceiteDaInscricaoRecord
{
    public Guid Id { get; set; }

    public Guid InscricaoId { get; set; }

    public int Ordem { get; set; }

    public string TermoCodigo { get; set; } = null!;

    public Guid VersaoId { get; set; }

    public string HashVersao { get; set; } = null!;
}
