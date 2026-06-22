namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;

public static class ReferenciaReservaDemograficaMapping
{
    public static ReferenciaReservaDemograficaDto ToDto(this ReferenciaReservaDemografica referencia)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        return new ReferenciaReservaDemograficaDto(
            referencia.Id,
            referencia.CensoReferencia,
            referencia.PpiPercentual.Valor,
            referencia.QuilombolaPercentual.Valor,
            referencia.PcdPercentual.Valor,
            referencia.BaseLegal,
            referencia.CreatedAt);
    }
}
