namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public static class TipoConceitoMapping
{
    public static TipoConceitoDto ToDto(this TipoConceito tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        bool numerico = tipo.Natureza == NaturezaConceito.Numerico;
        return new TipoConceitoDto(
            tipo.Id, tipo.Codigo, tipo.Nome, tipo.Descricao, NaturezasConceito.ParaContrato(tipo.Natureza), tipo.Ativo,
            numerico ? new EscalaNumericaDto(tipo.EscalaMinima!.Value, tipo.EscalaMaxima!.Value, tipo.CasasDecimais!.Value) : null,
            numerico ? null : [.. tipo.Valores.Select(valor => new ValorCategoricoDto(valor.Codigo, valor.Rotulo, valor.ContaComoAprovacao))]);
    }
}
