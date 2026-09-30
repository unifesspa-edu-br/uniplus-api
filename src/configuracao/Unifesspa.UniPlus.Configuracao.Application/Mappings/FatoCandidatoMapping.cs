namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

internal static class FatoCandidatoMapping
{
    public static FatoCandidatoDto ToDto(this FatoCandidato fato)
    {
        ArgumentNullException.ThrowIfNull(fato);
        return new FatoCandidatoDto(
            fato.Id,
            fato.Codigo,
            fato.Nome,
            fato.Descricao,
            DominiosFato.ParaTokenCanonico(fato.Dominio),
            OrigensFato.ParaTokenCanonico(fato.Origem),
            CardinalidadesFato.ParaTokenCanonico(fato.Cardinalidade),
            fato.FonteValores is { } fonte ? FontesValoresFato.ParaTokenCanonico(fonte) : null,
            fato.Formato is { } formato ? FormatosTexto.ParaTokenCanonico(formato) : null,
            fato.PontoResolucao,
            fato.Binding,
            EscoposFato.ParaTokenCanonico(fato.Escopo),
            ClassificacoesProtecaoDado.ParaTokenCanonico(fato.ClassificacaoProtecao),
            fato.FinalidadeTratamento,
            HipotesesLegaisTratamento.ParaTokenCanonico(fato.HipoteseLegal),
            fato.Sistema,
            fato.Ativo,
            [.. fato.ValoresDominioDeclarados
                .OrderBy(static v => v.Ordem)
                .ThenBy(static v => v.Codigo, StringComparer.Ordinal)
                .Select(static v => new FatoValorDominioDto(v.Codigo, v.Descricao, v.Ordem, v.Ativo))]);
    }
}
