namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

internal static class ApoioDaConfiguracaoClassificacao
{
    internal static ReferenciaRegra RegraCalculoMediaPonderada() =>
        ReferenciaRegra.De(RegraCalculoCodigo.FormulaMediaPonderada, "v1", new string('a', 64));

    internal static ReferenciaRegra RegraCalculoImportada() =>
        ReferenciaRegra.De(RegraCalculoCodigo.ClassificacaoImportada, "v1", new string('b', 64));

    internal static ReferenciaRegra RegraArredondamento() =>
        ReferenciaRegra.De(RegraArredondamentoCodigo.PrecisaoTruncar, "v1", new string('c', 64));

    internal static ReferenciaRegra RegraOrdemAlocacao() =>
        ReferenciaRegra.De(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", new string('d', 64));

    internal static DeclaracaoDaClassificacao DeclaracaoMediaPonderada() =>
        new()
        {
            RegraCalculo = RegraCalculoMediaPonderada(),
            RegraArredondamento = RegraArredondamento(),
            CasasArredondamento = 2,
            RegraOrdemAlocacao = RegraOrdemAlocacao(),
        };

    internal static DeclaracaoDaClassificacao DeclaracaoEnemLocal() =>
        DeclaracaoMediaPonderada() with
        {
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Completo(),
        };

    internal static DeclaracaoDaClassificacao DeclaracaoImportada() =>
        new() { RegraCalculo = RegraCalculoImportada(), RegraOrdemAlocacao = RegraOrdemAlocacao() };

    internal static RegraEliminacao Eliminacao(string codigoRegra) =>
        RegraEliminacao.Criar(
            ReferenciaRegra.De(codigoRegra, "v1", new string('a', 64)),
            ArgsDaRegra(codigoRegra)).Value!;

    internal static RegraEliminacao[] Eliminacoes(params string[] codigosDeRegra) =>
        [.. codigosDeRegra.Select(Eliminacao)];

    internal static ArgsRegraEliminacao ArgsDaRegra(string codigoRegra) => codigoRegra switch
    {
        RegraEliminacaoCodigo.ElimCorteEmArea => new ArgsElimCorteEmArea("REDACAO", 400m),
        RegraEliminacaoCodigo.ElimZeroEmArea => new ArgsElimZeroEmArea(),
        RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem => new ArgsElimFaltaEmDiaDeProvaEnem(),
        _ => throw new ArgumentOutOfRangeException(nameof(codigoRegra), codigoRegra, "Código de regra ENEM desconhecido no teste."),
    };

    internal static RegraEliminacao AusenciaNaEtapa(Guid etapaId) =>
        RegraEliminacao.Criar(
            ReferenciaRegra.De(RegraEliminacaoCodigo.ElimAusenciaNaEtapa, "v1", new string('a', 64)),
            new ArgsElimAusenciaNaEtapa(etapaId)).Value!;

    // ── Corte em área: no máximo um por área, e a área no quadro de pesos por área ──

    internal static RegraEliminacao CorteEm(string area, decimal minimo = 400m) =>
        RegraEliminacao.Criar(
            ReferenciaRegra.De(RegraEliminacaoCodigo.ElimCorteEmArea, "v1", new string('a', 64)),
            new ArgsElimCorteEmArea(area, minimo)).Value!;

    internal static Result<ConfiguracaoClassificacao> CriarEnemLocal(params RegraEliminacao[] eliminacoes) =>
        ConfiguracaoClassificacao.Criar(DeclaracaoEnemLocal() with { RegrasEliminacao = eliminacoes });
}
