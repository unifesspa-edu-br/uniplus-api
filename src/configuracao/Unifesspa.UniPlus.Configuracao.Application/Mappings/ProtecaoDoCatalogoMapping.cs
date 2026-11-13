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
            aprovacao.Ciclo.Numero,
            aprovacao.SolicitadaEm,
            aprovacao.DecididaEm,
            Token(aprovacao.ClassificacaoRecomendada),
            aprovacao.Justificativa,
            aprovacao.AceitaEm,
            aprovacao.ManifestadaEm,
            aprovacao.ManifestacaoDesfavoravelVigente,
            aprovacao.ManifestacoesNoCiclo);
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

    /// <summary>O detalhe do tipo de documento, com os atos registrados e as manifestações vigentes do ciclo corrente.</summary>
    public static DetalheDaProtecaoDoCatalogoDto ToDetalheDaProtecao(
        this TipoDocumento tipo, IReadOnlyList<AtoDaProtecaoDoCatalogo> atos, ManifestacoesDoCiclo manifestacoes)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        ArgumentNullException.ThrowIfNull(atos);
        ArgumentNullException.ThrowIfNull(manifestacoes);

        // Agrupados por assunto: o item, a proteção, o circuito e o que o tipo pede ao candidato.
        return new DetalheDaProtecaoDoCatalogoDto(
            TipoDocumento, tipo.Id, tipo.Codigo.Valor, tipo.Nome, tipo.Descricao,
            ClassificacoesDeProtecao.ParaTokenCanonico(tipo.ClassificacaoProtecao), HipotesesLegaisTratamento.ParaTokenCanonico(tipo.HipoteseLegal), tipo.FinalidadeTratamento,
            tipo.Aprovacao.ToDto(),
            Fato: null,
            new DetalheDoTipoDeDocumentoDto(tipo.Categoria, tipo.FormatosAceitos, tipo.TamanhoMaximoMb),
            [.. atos.Select(static a => a.ToDto())],
            manifestacoes.ToVigentesDto());
    }

    /// <summary>O detalhe do fato, com os atos registrados e as manifestações vigentes do ciclo corrente.</summary>
    public static DetalheDaProtecaoDoCatalogoDto ToDetalheDaProtecao(
        this FatoCandidato fato, IReadOnlyList<AtoDaProtecaoDoCatalogo> atos, ManifestacoesDoCiclo manifestacoes)
    {
        ArgumentNullException.ThrowIfNull(fato);
        ArgumentNullException.ThrowIfNull(atos);
        ArgumentNullException.ThrowIfNull(manifestacoes);

        // Agrupados por assunto: o item, a proteção, o circuito e o que é o fato.
        return new DetalheDaProtecaoDoCatalogoDto(
            Fato, fato.Id, fato.Codigo, fato.Nome, fato.Descricao,
            ClassificacoesDeProtecao.ParaTokenCanonico(fato.ClassificacaoProtecao), HipotesesLegaisTratamento.ParaTokenCanonico(fato.HipoteseLegal), fato.FinalidadeTratamento,
            fato.Aprovacao.ToDto(),
            fato.ToDetalheDoFato(),
            TipoDocumento: null,
            [.. atos.Select(static a => a.ToDto())],
            manifestacoes.ToVigentesDto());
    }

    public static AtoDaProtecaoDto ToDto(this AtoDaProtecaoDoCatalogo ato)
    {
        ArgumentNullException.ThrowIfNull(ato);

        // Agrupados por assunto: o ato, a proteção que ele retratou e os fundamentos da decisão.
        return new AtoDaProtecaoDto(
            AtosDaProtecao.ParaTokenCanonico(ato.Ato), ato.Instante, ato.AtorNome, ato.Ciclo.Numero,
            ClassificacoesDeProtecao.ParaTokenCanonico(ato.ClassificacaoProtecao), HipotesesLegaisTratamento.ParaTokenCanonico(ato.HipoteseLegal), ato.FinalidadeTratamento,
            Token(ato.ClassificacaoRecomendada), ato.Justificativa, ato.DianteDeManifestacaoDesfavoravel);
    }

    public static DecisaoDaProtecaoDto ToDecisaoDto(this AtoDaProtecaoDoCatalogo ato)
    {
        ArgumentNullException.ThrowIfNull(ato);
        AtoDaProtecaoDto dto = ato.ToDto();

        // Agrupados por assunto: o item no instante da decisão, o ato, a proteção e os fundamentos.
        return new DecisaoDaProtecaoDto(
            CadastrosDoCatalogo.ParaTokenCanonico(ato.Cadastro), ato.ItemId, ato.ItemCodigo, ato.ItemNome,
            dto.Ato, dto.Instante, dto.AtorNome, dto.Ciclo,
            dto.ClassificacaoProtecao, dto.HipoteseLegal, dto.FinalidadeTratamento,
            dto.ClassificacaoRecomendada, dto.Justificativa, dto.DianteDeManifestacaoDesfavoravel);
    }

    private static DetalheDoFatoDto ToDetalheDoFato(this FatoCandidato fato) =>
        new(
            DominiosFato.ParaTokenCanonico(fato.Dominio),
            CardinalidadesFato.ParaTokenCanonico(fato.Cardinalidade),
            fato.Sistema,
            fato.ComponenteExibicao is { } componente ? ComponentesDeExibicao.ParaTokenCanonico(componente) : null,
            [.. fato.ValoresDominioDeclarados
                .Where(static v => v.Ativo)
                .OrderBy(static v => v.Ordem)
                .ThenBy(static v => v.Codigo, StringComparer.Ordinal)
                .Select(static v => new DetalheDoValorDoFatoDto(v.Codigo, v.Descricao, v.Orientacao))]);

    private static string? Token(ClassificacaoDeProtecao? classificacao) =>
        classificacao is { } valor ? ClassificacoesDeProtecao.ParaTokenCanonico(valor) : null;
}
