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

    public static DetalheDaProtecaoDoCatalogoDto ToDetalheDaProtecao(this TipoDocumento tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        return new DetalheDaProtecaoDoCatalogoDto(
            TipoDocumento,
            tipo.Id,
            tipo.Codigo.Valor,
            tipo.Nome,
            tipo.Descricao,
            ClassificacoesDeProtecao.ParaTokenCanonico(tipo.ClassificacaoProtecao),
            HipotesesLegaisTratamento.ParaTokenCanonico(tipo.HipoteseLegal),
            tipo.FinalidadeTratamento,
            tipo.Aprovacao.ToDto(),
            Fato: null,
            new DetalheDoTipoDeDocumentoDto(tipo.Categoria, tipo.FormatosAceitos, tipo.TamanhoMaximoMb));
    }

    public static DetalheDaProtecaoDoCatalogoDto ToDetalheDaProtecao(this FatoCandidato fato)
    {
        ArgumentNullException.ThrowIfNull(fato);
        return new DetalheDaProtecaoDoCatalogoDto(
            Fato,
            fato.Id,
            fato.Codigo,
            fato.Nome,
            fato.Descricao,
            ClassificacoesDeProtecao.ParaTokenCanonico(fato.ClassificacaoProtecao),
            HipotesesLegaisTratamento.ParaTokenCanonico(fato.HipoteseLegal),
            fato.FinalidadeTratamento,
            fato.Aprovacao.ToDto(),
            new DetalheDoFatoDto(
                DominiosFato.ParaTokenCanonico(fato.Dominio),
                CardinalidadesFato.ParaTokenCanonico(fato.Cardinalidade),
                fato.Sistema,
                fato.ComponenteExibicao is { } componente ? ComponentesDeExibicao.ParaTokenCanonico(componente) : null,
                [.. fato.ValoresDominioDeclarados
                    .Where(static v => v.Ativo)
                    .OrderBy(static v => v.Ordem)
                    .ThenBy(static v => v.Codigo, StringComparer.Ordinal)
                    .Select(static v => new DetalheDoValorDoFatoDto(v.Codigo, v.Descricao, v.Orientacao))]),
            TipoDocumento: null);
    }
}
