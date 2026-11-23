namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

public sealed record ProvaPresencialCertameDto(bool TemProvaPresencial, IReadOnlyList<MunicipioDeProvaCertameDto> Municipios);

public sealed record MunicipioDeProvaCertameDto(string CodigoIbge, string Uf, string Nome);
