namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public static class BaseLegalBonusRegionalMapping
{
    public static BaseLegalBonusRegionalDto ToDto(this BaseLegalBonusRegional entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return new BaseLegalBonusRegionalDto(
            entity.Id,
            entity.TipoInstrumento.ToCodigo(),
            entity.Identificacao,
            entity.Descricao,
            entity.Municipios.Select(m => new BaseLegalBonusRegionalMunicipioDto(m.CodigoIbge, m.Nome, m.Uf)).ToList().AsReadOnly(),
            entity.CreatedAt);
    }
}
