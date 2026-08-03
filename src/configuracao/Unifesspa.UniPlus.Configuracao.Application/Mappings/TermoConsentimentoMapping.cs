namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

public static class TermoConsentimentoMapping
{
    public static TermoConsentimentoDto ToDto(this TermoConsentimento termo)
    {
        ArgumentNullException.ThrowIfNull(termo);
        return new TermoConsentimentoDto(
            termo.Id,
            termo.Nome,
            termo.TextoRascunho,
            termo.BaseLegalRascunho,
            FormasAceite.ParaTokenCanonico(termo.FormaAceiteRascunho),
            termo.Revisado,
            termo.RevisadoEm,
            [.. termo.Versoes.Select(ToDto)],
            termo.CreatedAt);
    }

    public static TermoConsentimentoResumoDto ToResumoDto(this TermoConsentimento termo)
    {
        ArgumentNullException.ThrowIfNull(termo);
        return new TermoConsentimentoResumoDto(
            termo.Id,
            termo.Nome,
            FormasAceite.ParaTokenCanonico(termo.FormaAceiteRascunho),
            termo.Revisado,
            termo.CreatedAt);
    }

    private static TermoConsentimentoVersaoDto ToDto(TermoConsentimentoVersao versao) =>
        new(
            versao.Id,
            versao.Texto,
            versao.BaseLegal,
            FormasAceite.ParaTokenCanonico(versao.FormaAceite),
            versao.Hash,
            versao.PromovidaEm);
}
