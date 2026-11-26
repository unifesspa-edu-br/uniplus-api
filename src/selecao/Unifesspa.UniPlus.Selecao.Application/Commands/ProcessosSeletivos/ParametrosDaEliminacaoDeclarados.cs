namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ParametrosDaEliminacaoDeclarados
{
    internal static Result<ParametrosRegraEliminacao> Montar(RegraEliminacaoInput input) =>
        CatalogoDe<RegraDeEliminacaoPorCalculo>.TentarObter(input.RegraCodigo, out RegraDeEliminacaoPorCalculo regra)
            ? DaRegra(input, regra)
            : RecusaDosParametros("RegraEliminacao.RegraTipoInvalido", "Código de regra de eliminação desconhecido.");

    private static Result<ParametrosRegraEliminacao> DaRegra(RegraEliminacaoInput input, RegraDeEliminacaoPorCalculo regra) =>
        regra switch
        {
            RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa => ParametrosDaNotaMinimaNaEtapa(input),
            RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem => ParametrosDoCorteEmArea(input),
            RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem => SemParametros(input, new ParametrosEliminacaoPorZeroEmAreaDoEnem()),
            RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem => SemParametros(input, new ParametrosEliminacaoPorFaltaEmDiaDeProvaDoEnem()),
            RegraDeEliminacaoPorCalculo.AusenciaNaEtapa => ParametrosDaAusenciaNaEtapa(input),
            _ => throw new ArgumentOutOfRangeException(nameof(regra)),
        };

    private static Result<ParametrosRegraEliminacao> ParametrosDaNotaMinimaNaEtapa(RegraEliminacaoInput input) =>
        input.EtapaRef is { } etapaRef && input.NotaMinima is { } notaMinima && input.Minimo is null && input.AreaCodigo is null
            ? Result<ParametrosRegraEliminacao>.Success(new ParametrosEliminacaoPorNotaMinimaNaEtapa(etapaRef, notaMinima))
            : RecusaDosParametros(
                "RegraEliminacao.EtapaRefENotaMinimaObrigatorios",
                $"EtapaRef e NotaMinima são obrigatórios (e Minimo/AreaCodigo não se aplicam) para a regra {RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa.Codigo()}.");

    private static Result<ParametrosRegraEliminacao> ParametrosDoCorteEmArea(RegraEliminacaoInput input) =>
        input.AreaCodigo is { } areaCodigo && input.Minimo is { } minimo && input.EtapaRef is null && input.NotaMinima is null
            ? Result<ParametrosRegraEliminacao>.Success(new ParametrosEliminacaoPorCorteEmAreaDoEnem(areaCodigo, minimo))
            : RecusaDosParametros(
                "RegraEliminacao.AreaEMinimoObrigatorios",
                $"AreaCodigo e Minimo são obrigatórios (e EtapaRef/NotaMinima não se aplicam) para a regra {RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem.Codigo()}.");

    private static Result<ParametrosRegraEliminacao> ParametrosDaAusenciaNaEtapa(RegraEliminacaoInput input) =>
        input.EtapaRef is { } etapaRef && input.NotaMinima is null && input.Minimo is null && input.AreaCodigo is null
            ? Result<ParametrosRegraEliminacao>.Success(new ParametrosEliminacaoPorAusenciaNaEtapa(etapaRef))
            : RecusaDosParametros(
                "RegraEliminacao.EtapaRefObrigatorio",
                $"EtapaRef é obrigatório (e NotaMinima/Minimo/AreaCodigo não se aplicam) para a regra {RegraDeEliminacaoPorCalculo.AusenciaNaEtapa.Codigo()}.");

    private static Result<ParametrosRegraEliminacao> SemParametros(RegraEliminacaoInput input, ParametrosRegraEliminacao parametros) =>
        input.EtapaRef is null && input.NotaMinima is null && input.Minimo is null && input.AreaCodigo is null
            ? Result<ParametrosRegraEliminacao>.Success(parametros)
            : RecusaDosParametros(
                "RegraEliminacao.ParametrosIncompativeisComRegra",
                "A regra informada não aceita parâmetros (EtapaRef/NotaMinima/Minimo/AreaCodigo).");

    private static Result<ParametrosRegraEliminacao> RecusaDosParametros(string codigo, string mensagem) =>
        Result<ParametrosRegraEliminacao>.Failure(new DomainError(codigo, mensagem));
}
