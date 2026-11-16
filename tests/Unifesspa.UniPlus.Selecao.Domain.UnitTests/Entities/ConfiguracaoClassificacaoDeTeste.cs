namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ConfiguracaoClassificacaoDeTeste
{
    internal static ReferenciaRegra RegraCalculoMediaPonderada() =>
        ReferenciaRegra.Criar(RegraCalculoCodigo.FormulaMediaPonderada, "v1", new string('a', 64)).Value!;

    internal static ReferenciaRegra RegraCalculoImportada() =>
        ReferenciaRegra.Criar(RegraCalculoCodigo.ClassificacaoImportada, "v1", new string('b', 64)).Value!;

    internal static ReferenciaRegra RegraArredondamento() =>
        ReferenciaRegra.Criar(RegraArredondamentoCodigo.PrecisaoTruncar, "v1", new string('c', 64)).Value!;

    internal static ReferenciaRegra RegraOrdemAlocacao() =>
        ReferenciaRegra.Criar(RegraOrdemAlocacaoCodigo.AlocacaoPrimeiraOpcaoPrioritaria, "v1", new string('d', 64)).Value!;
}
