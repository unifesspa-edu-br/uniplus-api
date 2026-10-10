namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

/// <summary>Uma ocorrência de grupo repetível da inscrição, pelo id estável que o formulário dá ao membro.</summary>
public sealed class OcorrenciaDeGrupoDaInscricaoRecord
{
    public Guid Id { get; set; }

    public Guid InscricaoId { get; set; }

    public string GrupoCodigo { get; set; } = null!;

    public string MembroId { get; set; } = null!;

    public int Ordem { get; set; }
}
