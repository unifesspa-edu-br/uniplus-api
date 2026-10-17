namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Records;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>A resposta de um campo de endereço da inscrição, um componente por coluna.</summary>
public sealed class EnderecoDaInscricaoRecord
{
    public Guid Id { get; set; }

    public Guid InscricaoId { get; set; }

    public Guid? OcorrenciaId { get; set; }

    public string FatoCodigo { get; set; } = null!;

    public OrigemDaResposta Origem { get; set; }

    public string? Cep { get; set; }

    public string? Logradouro { get; set; }

    public string? Numero { get; set; }

    public string? Complemento { get; set; }

    public string? Bairro { get; set; }

    public string? Distrito { get; set; }

    public string? MunicipioCodigoIbge { get; set; }

    public string? MunicipioNome { get; set; }

    public string? Uf { get; set; }

    public string? Latitude { get; set; }

    public string? Longitude { get; set; }

    public string? NivelResolucao { get; set; }

    public string? Procedencia { get; set; }

    public bool? CepConfirmado { get; set; }
}
