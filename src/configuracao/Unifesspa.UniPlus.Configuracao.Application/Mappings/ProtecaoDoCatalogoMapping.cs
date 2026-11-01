namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

internal static class ProtecaoDoCatalogoMapping
{
    public const string TipoDocumento = CadastrosDaProtecaoDoCatalogo.TipoDocumento;
    public const string Fato = CadastrosDaProtecaoDoCatalogo.Fato;

    public static AprovacaoDaProtecaoDto ToDto(this AprovacaoDaProtecao aprovacao)
    {
        ArgumentNullException.ThrowIfNull(aprovacao);
        return new AprovacaoDaProtecaoDto(
            SituacoesDaAprovacao.ParaTokenCanonico(aprovacao.Situacao),
            aprovacao.SolicitadaEm,
            aprovacao.DecididaEm,
            aprovacao.ClassificacaoRecomendada is { } recomendada ? ClassificacoesDeProtecao.ParaTokenCanonico(recomendada) : null,
            aprovacao.Justificativa,
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

    public static DetalheDaProtecaoDoCatalogoDto ToDetalheDaProtecao(this TipoDocumento tipo, IReadOnlyList<AtoDaProtecaoDoCatalogo> atos)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        ArgumentNullException.ThrowIfNull(atos);
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
            new DetalheDoTipoDeDocumentoDto(tipo.Categoria, tipo.FormatosAceitos, tipo.TamanhoMaximoMb),
            [.. atos.Select(static a => a.ToDto())]);
    }

    public static DetalheDaProtecaoDoCatalogoDto ToDetalheDaProtecao(this FatoCandidato fato, IReadOnlyList<AtoDaProtecaoDoCatalogo> atos)
    {
        ArgumentNullException.ThrowIfNull(fato);
        ArgumentNullException.ThrowIfNull(atos);
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
            TipoDocumento: null,
            [.. atos.Select(static a => a.ToDto())]);
    }

    public static AtoDaProtecaoDto ToDto(this AtoDaProtecaoDoCatalogo ato)
    {
        ArgumentNullException.ThrowIfNull(ato);
        return new AtoDaProtecaoDto(
            AtosDaProtecao.ParaTokenCanonico(ato.Ato),
            ato.Instante,
            ato.AtorNome,
            ClassificacoesDeProtecao.ParaTokenCanonico(ato.ClassificacaoProtecao),
            HipotesesLegaisTratamento.ParaTokenCanonico(ato.HipoteseLegal),
            ato.FinalidadeTratamento,
            ato.ClassificacaoRecomendada is { } recomendada ? ClassificacoesDeProtecao.ParaTokenCanonico(recomendada) : null,
            ato.Justificativa);
    }

    public static DecisaoDaProtecaoDto ToDecisaoDto(this AtoDaProtecaoDoCatalogo ato)
    {
        ArgumentNullException.ThrowIfNull(ato);
        AtoDaProtecaoDto dto = ato.ToDto();
        return new DecisaoDaProtecaoDto(
            CadastrosDoCatalogo.ParaTokenCanonico(ato.Cadastro),
            ato.ItemId,
            ato.ItemCodigo,
            ato.ItemNome,
            dto.Ato,
            dto.Instante,
            dto.AtorNome,
            dto.ClassificacaoProtecao,
            dto.HipoteseLegal,
            dto.FinalidadeTratamento,
            dto.ClassificacaoRecomendada,
            dto.Justificativa);
    }
}
