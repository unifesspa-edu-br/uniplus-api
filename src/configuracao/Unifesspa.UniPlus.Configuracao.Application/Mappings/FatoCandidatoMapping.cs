namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

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
        fato.ComponenteExibicao?.Codigo());

    private static DefinicaoDoFatoDto DefinicaoDe(FatoCandidato fato) => new(
        CodigoComRotulo<TipoDoDado>.De(fato.Dominio),
        CodigoComRotulo<QuantidadeDeValores>.De(fato.Cardinalidade),
        fato.Escopo.Codigo(),
        fato.Formato?.Codigo(),
        ValoresDe(fato),
        new ResolucaoDoFatoDto(CodigoComRotulo<OrigemFato>.De(fato.Origem), fato.PontoResolucao, fato.Binding),
        ProtecaoDe(fato));

    private static ValoresDoFatoDto ValoresDe(FatoCandidato fato) => new(
        fato.FonteValores is { } fonte ? CodigoComRotulo<FonteValoresFato>.De(fonte) : null,
        [.. fato.ValoresDominioDeclarados
            .OrderBy(static v => v.Ordem)
            .ThenBy(static v => v.Codigo, StringComparer.Ordinal)
            .Select(static v => new FatoValorDominioDto(v.Codigo, v.Descricao, v.Ordem, v.Ativo, v.Orientacao))]);

    private static ProtecaoDoFatoDto ProtecaoDe(FatoCandidato fato) => new(
        ClassificacoesDeProtecao.ParaTokenCanonico(fato.ClassificacaoProtecao),
        fato.FinalidadeTratamento,
        fato.HipoteseLegal.Codigo(),
        fato.Aprovacao.ToDto());
}
