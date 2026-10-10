namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Um valor respondido na inscrição. Só a coluna do <see cref="Tipo"/> fica preenchida, ou
/// <see cref="ValorCifrado"/> quando o fato é número de documento.
/// </summary>
public sealed class RespostaDaInscricaoRecord
{
    public Guid Id { get; set; }

    public Guid InscricaoId { get; set; }

    /// <summary>A ocorrência do grupo repetível; nula no nível superior do formulário.</summary>
    public Guid? OcorrenciaId { get; set; }

    public string FatoCodigo { get; set; } = null!;

    /// <summary>A posição do valor num fato multivalorado; nula num fato de valor único.</summary>
    public int? Posicao { get; set; }

    public OrigemDaResposta Origem { get; set; }

    public TipoDoValorDeResposta Tipo { get; set; }

    public bool? ValorBooleano { get; set; }

    public decimal? ValorNumero { get; set; }

    public string? ValorCodigo { get; set; }

    public string? ValorTexto { get; set; }

    public DateOnly? ValorData { get; set; }

    /// <summary>Envelope AEAD do valor em JSON — nunca texto claro.</summary>
    public byte[]? ValorCifrado { get; set; }
}
