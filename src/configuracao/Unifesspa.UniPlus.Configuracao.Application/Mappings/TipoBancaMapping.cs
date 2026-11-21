namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Regras.Enums;

public static class TipoBancaMapping
{
    public static TipoBancaDto ToDto(this TipoBanca banca)
    {
        ArgumentNullException.ThrowIfNull(banca);
        return new TipoBancaDto(
            banca.Id,
            CodigoComRotulo<TipoDeBanca>.De(banca.Codigo),
            banca.Nome,
            banca.FaseTipica,
            banca.Descricao,
            banca.CreatedAt);
    }
}
