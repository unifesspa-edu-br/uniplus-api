namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
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
            tipo.Id, tipo.Codigo, tipo.Ativo,
            new DefinicaoDoTipoConceitoDto(
                tipo.Nome, tipo.Descricao, CodigoComRotulo<NaturezaConceito>.De(tipo.Natureza),
                numerico ? new EscalaNumericaDto(tipo.EscalaMinima!.Value, tipo.EscalaMaxima!.Value, tipo.CasasDecimais!.Value) : null,
                numerico ? null : [.. tipo.Valores.Select(valor => new ValorCategoricoDto(valor.Codigo, valor.Rotulo, valor.ContaComoAprovacao))]));
    }
}
