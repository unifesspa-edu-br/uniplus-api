namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;

public static class TipoBancaMapping
{
    public static TipoBancaDto ToDto(this TipoBanca banca)
    {
        ArgumentNullException.ThrowIfNull(banca);
        return new TipoBancaDto(
            banca.Id,
            banca.Codigo.Valor,
            banca.Nome,
            banca.FaseTipica,
            banca.Descricao,
            banca.CreatedAt);
    }
}
