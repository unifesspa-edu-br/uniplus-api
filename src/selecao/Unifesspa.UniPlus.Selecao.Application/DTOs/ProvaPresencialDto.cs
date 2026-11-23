namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

public sealed record ProvaPresencialDto(bool TemProvaPresencial, IReadOnlyList<MunicipioDeProvaDto> Municipios);

public sealed record MunicipioDeProvaDto(string CodigoIbge, string Uf, string? Nome);
