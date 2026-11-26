namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public static class BaseLegalDoFadaMapping
{
    public static BaseLegalDoFadaDto ToDto(this BaseLegalDoFada entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return new BaseLegalDoFadaDto(
            entity.Id,
            entity.Codigo.Valor,
            CodigoComRotulo<TipoInstrumentoNormativo>.De(entity.TipoInstrumento),
            entity.Identificacao,
            entity.Descricao,
            entity.Municipios.Select(m => new MunicipioDaBaseLegalDoFadaDto(m.CodigoIbge, m.Nome, m.Uf)).ToList().AsReadOnly(),
            entity.CreatedAt);
    }
}
