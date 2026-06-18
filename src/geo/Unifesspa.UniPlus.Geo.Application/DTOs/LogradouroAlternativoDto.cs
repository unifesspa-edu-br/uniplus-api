namespace Unifesspa.UniPlus.Geo.Application.DTOs;

/// <summary>
/// Logradouro alternativo de um CEP que casa vários logradouros — entra em
/// <see cref="CepResolvidoDto.Alternativos"/> (o primário fica nos campos de topo
/// do <see cref="CepResolvidoDto"/>). A escolha do primário e a ordem dos
/// alternativos seguem o desempate estável <c>(nome_normalizado, distrito_id,
/// bairro_id, Id)</c>, determinístico entre execuções.
/// </summary>
/// <remarks>
/// "Alternativo" (não "Candidato") evita colisão com o conceito de domínio
/// <em>Candidato</em> — a pessoa inscrita num processo seletivo.
/// </remarks>
public sealed record LogradouroAlternativoDto(
    string? Tipo,
    string Logradouro,
    string? Bairro,
    string? Distrito,
    decimal? Latitude,
    decimal? Longitude);
