namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

internal static class ProtecaoDoCatalogoMapping
{
    public const string TipoDocumento = "TIPO_DOCUMENTO";
    public const string Fato = "FATO";

    public static AprovacaoDaProtecaoDto ToDto(this AprovacaoDaProtecao aprovacao)
    {
        ArgumentNullException.ThrowIfNull(aprovacao);
        return new AprovacaoDaProtecaoDto(
            SituacoesDaAprovacao.ParaTokenCanonico(aprovacao.Situacao),
            aprovacao.SolicitadaEm,
            aprovacao.DecididaEm,
            aprovacao.ClassificacaoRecomendada is { } recomendada ? ClassificacoesDeProtecao.ParaTokenCanonico(recomendada) : null,
            aprovacao.AceitaEm);
    }

    public static ItemDaProtecaoDoCatalogoDto ToItemDaProtecao(this TipoDocumento tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        return new ItemDaProtecaoDoCatalogoDto(
            TipoDocumento,
            tipo.Id,
            tipo.Codigo.Valor,
            tipo.Nome,
            ClassificacoesDeProtecao.ParaTokenCanonico(tipo.ClassificacaoProtecao),
            tipo.FinalidadeTratamento,
            HipotesesLegaisTratamento.ParaTokenCanonico(tipo.HipoteseLegal),
            tipo.Aprovacao.ToDto());
    }

    public static ItemDaProtecaoDoCatalogoDto ToItemDaProtecao(this FatoCandidato fato)
    {
        ArgumentNullException.ThrowIfNull(fato);
        return new ItemDaProtecaoDoCatalogoDto(
            Fato,
            fato.Id,
            fato.Codigo,
            fato.Nome,
            ClassificacoesDeProtecao.ParaTokenCanonico(fato.ClassificacaoProtecao),
            fato.FinalidadeTratamento,
            HipotesesLegaisTratamento.ParaTokenCanonico(fato.HipoteseLegal),
            fato.Aprovacao.ToDto());
    }
}
