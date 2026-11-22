namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Regras.Enums;

public static class TermoConsentimentoMapping
{
    public static TermoConsentimentoDto ToDto(this TermoConsentimento termo)
    {
        ArgumentNullException.ThrowIfNull(termo);
        return new TermoConsentimentoDto(
            termo.Id,
            termo.Codigo.Valor,
            termo.Nome,
            termo.TextoRascunho,
            termo.BaseLegalRascunho,
            CodigoComRotulo<FormaAceite>.De(termo.FormaAceiteRascunho),
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
            termo.Codigo.Valor,
            termo.Nome,
            CodigoComRotulo<FormaAceite>.De(termo.FormaAceiteRascunho),
            termo.Revisado,
            termo.CreatedAt);
    }

    private static TermoConsentimentoVersaoDto ToDto(TermoConsentimentoVersao versao) =>
        new(
            versao.Id,
            versao.Numero,
            versao.Texto,
            versao.BaseLegal,
            CodigoComRotulo<FormaAceite>.De(versao.FormaAceite),
            versao.Hash,
            versao.PromovidaEm);
}
