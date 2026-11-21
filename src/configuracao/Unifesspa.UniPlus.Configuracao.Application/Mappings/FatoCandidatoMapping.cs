namespace Unifesspa.UniPlus.Configuracao.Application.Mappings;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Regras.Enums;

internal static class FatoCandidatoMapping
{
    public static FatoCandidatoDto ToDto(this FatoCandidato fato)
    {
        ArgumentNullException.ThrowIfNull(fato);
        // identificação, exibição, domínio/origem/cardinalidade, fonte e formato, resolução, proteção, estado, valores, regras, aprovação, operadores
        return new FatoCandidatoDto(
            fato.Id, fato.Codigo, fato.Nome, fato.Descricao,
            fato.ComponenteExibicao is { } componente ? ComponentesDeExibicao.ParaTokenCanonico(componente) : null,
            DominiosFato.ParaTokenCanonico(fato.Dominio), OrigensFato.ParaTokenCanonico(fato.Origem), CardinalidadesFato.ParaTokenCanonico(fato.Cardinalidade),
            fato.FonteValores is { } fonte ? FontesValoresFato.ParaTokenCanonico(fonte) : null, fato.Formato is { } formato ? FormatosTexto.ParaTokenCanonico(formato) : null,
            fato.PontoResolucao, fato.Binding, EscoposFato.ParaTokenCanonico(fato.Escopo),
            ClassificacoesDeProtecao.ParaTokenCanonico(fato.ClassificacaoProtecao), fato.FinalidadeTratamento, HipotesesLegaisTratamento.ParaTokenCanonico(fato.HipoteseLegal),
            fato.SemeadoPeloSistema, fato.Ativo,
            fato.ValoresEmOrdem(),
            [.. fato.RegrasPadrao.Select(static r => new RegraPadraoDto(r.Contribui, r.Quando.ToDto()))],
            fato.Aprovacao.ToDto(),
            fato.OperadoresAdmitidos());
    }

    private static List<FatoValorDominioDto> ValoresEmOrdem(this FatoCandidato fato) =>
        [.. fato.ValoresDominioDeclarados
            .OrderBy(static v => v.Ordem)
            .ThenBy(static v => v.Codigo, StringComparer.Ordinal)
            .Select(static v => new FatoValorDominioDto(v.Codigo, v.Descricao, v.Ordem, v.Ativo, v.Orientacao))];

    private static List<CodigoComRotulo<Operador>> OperadoresAdmitidos(this FatoCandidato fato)
    {
        if (VocabularioDoCatalogo.TipoNoVocabulario(fato) is not { } dominio)
        {
            return [];
        }

        CardinalidadeDoFato cardinalidade = VocabularioDoCatalogo.CardinalidadeNoVocabulario(fato);
        return [.. OperadoresDoFato.Admitidos(dominio, cardinalidade)
            .Select(operador => CodigoComRotulo<Operador>.De(operador, OperadoresDoFato.Rotulo(operador, cardinalidade)))];
    }
}
