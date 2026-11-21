namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;

/// <summary>A declaração de um local de oferta depois de conferida: cidade sem espaço nas pontas, UF em maiúsculas e código e-MEC ausente quando vazio.</summary>
internal sealed record CamposDoLocalDeOferta(
    TipoLocalOferta Tipo,
    Guid? CampusResponsavelId,
    string CidadeCodigoIbge,
    string CidadeNome,
    string CidadeUf,
    ReferenciaEnderecoGeo? Endereco,
    string? CodigoEmec);
