namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Classificacao;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A forma dos parâmetros de uma eliminação declarada: cada regra exige uns e não aceita outros. Vale tanto para o pedido
/// de definição da classificação quanto para a leitura do documento, para que as duas recusem a mesma forma com o mesmo texto.
/// </summary>
public static class ParametrosDeclaradosDaEliminacao
{
    /// <summary>Código da recusa do código de regra que o sistema não conhece; quem chama o atribui ao campo que cita o código.</summary>
    public const string CodigoDeRegraDesconhecido = "RegraEliminacao.RegraTipoInvalido";

    public static Result<ParametrosRegraEliminacao> Montar(
        string regraCodigo, Guid? etapaRef, decimal? notaMinima, decimal? minimo, string? areaCodigo)
    {
        ParametrosDeclarados declarados = new(etapaRef, notaMinima, minimo, areaCodigo);
        return CatalogoDe<RegraDeEliminacaoPorCalculo>.TentarObter(regraCodigo, out RegraDeEliminacaoPorCalculo regra)
            ? DaRegra(regra, declarados)
            : Falha(CodigoDeRegraDesconhecido, "O código da regra de eliminação não é conhecido.");
    }

    private static Result<ParametrosRegraEliminacao> DaRegra(RegraDeEliminacaoPorCalculo regra, ParametrosDeclarados declarados) =>
        regra switch
        {
            RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa => NotaMinimaNaEtapa(declarados),
            RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem => CorteEmArea(declarados),
            RegraDeEliminacaoPorCalculo.AusenciaNaEtapa => AusenciaNaEtapa(declarados),
            RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem => SemParametros(declarados, new ParametrosEliminacaoPorZeroEmAreaDoEnem()),
            RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem => SemParametros(declarados, new ParametrosEliminacaoPorFaltaEmDiaDeProvaDoEnem()),
            _ => throw new ArgumentOutOfRangeException(nameof(regra)),
        };

    private static Result<ParametrosRegraEliminacao> NotaMinimaNaEtapa(ParametrosDeclarados declarados) =>
        declarados is { EtapaRef: { } etapa, NotaMinima: { } notaMinima, Minimo: null, AreaCodigo: null }
            ? Sucesso(new ParametrosEliminacaoPorNotaMinimaNaEtapa(etapa, notaMinima))
            : Falha("RegraEliminacao.EtapaRefENotaMinimaObrigatorios", "A regra de eliminação por nota mínima exige a etapa e a nota mínima. Informe os dois e retire o mínimo e a área.");

    private static Result<ParametrosRegraEliminacao> CorteEmArea(ParametrosDeclarados declarados) =>
        declarados is { AreaCodigo: { } area, Minimo: { } minimo, EtapaRef: null, NotaMinima: null }
            ? Sucesso(new ParametrosEliminacaoPorCorteEmAreaDoEnem(area, minimo))
            : Falha("RegraEliminacao.AreaEMinimoObrigatorios", "A regra de eliminação por corte em área exige a área e o mínimo. Informe os dois e retire a etapa e a nota mínima.");

    private static Result<ParametrosRegraEliminacao> AusenciaNaEtapa(ParametrosDeclarados declarados) =>
        declarados is { EtapaRef: { } etapa, NotaMinima: null, Minimo: null, AreaCodigo: null }
            ? Sucesso(new ParametrosEliminacaoPorAusenciaNaEtapa(etapa))
            : Falha("RegraEliminacao.EtapaRefObrigatorio", "A regra de eliminação por ausência exige a etapa. Informe a etapa e retire a nota mínima, o mínimo e a área.");

    private static Result<ParametrosRegraEliminacao> SemParametros(ParametrosDeclarados declarados, ParametrosRegraEliminacao parametros) =>
        declarados is { EtapaRef: null, NotaMinima: null, Minimo: null, AreaCodigo: null }
            ? Sucesso(parametros)
            : Falha("RegraEliminacao.ParametrosIncompativeisComRegra", "A regra de eliminação não aceita parâmetros. Retire os parâmetros da regra.");

    private static Result<ParametrosRegraEliminacao> Sucesso(ParametrosRegraEliminacao parametros) =>
        Result<ParametrosRegraEliminacao>.Success(parametros);

    private static Result<ParametrosRegraEliminacao> Falha(string codigo, string mensagem) =>
        Result<ParametrosRegraEliminacao>.Failure(new DomainError(codigo, mensagem));

    private readonly record struct ParametrosDeclarados(Guid? EtapaRef, decimal? NotaMinima, decimal? Minimo, string? AreaCodigo);
}
