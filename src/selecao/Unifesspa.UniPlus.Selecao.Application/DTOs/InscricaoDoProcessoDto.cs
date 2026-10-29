namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Uma inscrição na consulta de quem opera o processo: o suficiente para identificar a pessoa e a situação, sem
/// documento e sem o CPF completo.
/// </summary>
/// <param name="Id">A identidade da inscrição.</param>
/// <param name="NumeroInscricao">O número gerado no primeiro envio.</param>
/// <param name="Situacao">Submetida ou cancelada.</param>
/// <param name="Nome">O nome social, quando o candidato o declarou; senão o nome civil.</param>
/// <param name="CpfMascarado">O CPF no padrão <c>***.999.999-**</c>; o completo só se revela pela permissão própria.</param>
/// <param name="SubmetidaEm">O instante do primeiro envio.</param>
public sealed record InscricaoDoProcessoDto(
    Guid Id,
    string? NumeroInscricao,
    SituacaoInscricao Situacao,
    string? Nome,
    string CpfMascarado,
    DateTimeOffset? SubmetidaEm);
