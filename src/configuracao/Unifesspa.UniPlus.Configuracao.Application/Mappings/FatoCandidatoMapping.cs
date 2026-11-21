namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

internal static class FatoCandidatoMapping
{
    public static FatoDeManutencaoDto ToDto(this FatoCandidato fato)
    {
        ArgumentNullException.ThrowIfNull(fato);
        return new FatoDeManutencaoDto(FatoDe(fato), OperadoresAdmitidosDoFato.De(fato));
    }

    private static FatoCandidatoDto FatoDe(FatoCandidato fato) => new(
        fato.Id,
        fato.Codigo,
        DescritivoDe(fato),
        DefinicaoDe(fato),
        [.. fato.RegrasPadrao.Select(static r => new RegraPadraoDto(r.Contribui, r.Quando.ToDto()))],
        fato.SemeadoPeloSistema,
        fato.Ativo);

    private static DescritivoDoFatoDto DescritivoDe(FatoCandidato fato) => new(
        fato.Nome,
        fato.Descricao,
        fato.ComponenteExibicao is { } componente ? ComponentesDeExibicao.ParaTokenCanonico(componente) : null);

    private static DefinicaoDoFatoDto DefinicaoDe(FatoCandidato fato) => new(
        DominiosFato.ParaTokenCanonico(fato.Dominio),
        CardinalidadesFato.ParaTokenCanonico(fato.Cardinalidade),
        EscoposFato.ParaTokenCanonico(fato.Escopo),
        fato.Formato is { } formato ? FormatosTexto.ParaTokenCanonico(formato) : null,
        ValoresDe(fato),
        new ResolucaoDoFatoDto(OrigensFato.ParaTokenCanonico(fato.Origem), fato.PontoResolucao, fato.Binding),
        ProtecaoDe(fato));

    private static ValoresDoFatoDto ValoresDe(FatoCandidato fato) => new(
        fato.FonteValores is { } fonte ? FontesValoresFato.ParaTokenCanonico(fonte) : null,
        [.. fato.ValoresDominioDeclarados
            .OrderBy(static v => v.Ordem)
            .ThenBy(static v => v.Codigo, StringComparer.Ordinal)
            .Select(static v => new FatoValorDominioDto(v.Codigo, v.Descricao, v.Ordem, v.Ativo, v.Orientacao))]);

    private static ProtecaoDoFatoDto ProtecaoDe(FatoCandidato fato) => new(
        ClassificacoesDeProtecao.ParaTokenCanonico(fato.ClassificacaoProtecao),
        fato.FinalidadeTratamento,
        HipotesesLegaisTratamento.ParaTokenCanonico(fato.HipoteseLegal),
        fato.Aprovacao.ToDto());
}
