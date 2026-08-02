namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public static class CalendarioDiasUteisMapping
{
    public static CalendarioDiasUteisDto ToDto(this CalendarioDiasUteis calendario)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        return new CalendarioDiasUteisDto(
            calendario.Id,
            calendario.VersaoDataset,
            calendario.Vigente,
            [.. calendario.DiasNaoUteis.Select(ToDto)],
            calendario.CreatedAt);
    }

    public static CalendarioDiasUteisResumoDto ToResumoDto(this CalendarioDiasUteis calendario)
    {
        ArgumentNullException.ThrowIfNull(calendario);
        return new CalendarioDiasUteisResumoDto(
            calendario.Id,
            calendario.VersaoDataset,
            calendario.Vigente,
            calendario.CreatedAt);
    }

    private static DiaNaoUtilDto ToDto(DiaNaoUtil dia) =>
        new(
            dia.Id,
            Abrangencias.ParaTokenCanonico(dia.Abrangencia),
            dia.MunicipioIbge,
            dia.Uf,
            dia.Data,
            dia.Descricao);
}
