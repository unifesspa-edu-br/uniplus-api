namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;

using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposConceito;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>Comandos e tipos de conceito válidos, a serem alterados com <c>with</c> em cada cenário.</summary>
internal static class TiposConceitoDeTeste
{
    public static CriarTipoConceitoCommand CriarNumerico() =>
        new("NOTA_ENTREVISTA", "Nota da entrevista", "Nota atribuída pela banca.", "NUMERICO", new EscalaNumericaCommand(0m, 10m, 2), null);

    public static CriarTipoConceitoCommand CriarCategorico() =>
        new("PARECER_ANALISE", "Parecer da análise", null, "CATEGORICO", null,
            [new ValorCategoricoCommand("DEFERIDO", "Deferido", true), new ValorCategoricoCommand("INDEFERIDO", "Indeferido", false)]);

    public static AtualizarTipoConceitoCommand AtualizarNumerico(Guid id) =>
        new(id, "Nota da entrevista revisada", null, "NUMERICO", new EscalaNumericaCommand(0m, 100m, 1), null);

    public static AtualizarTipoConceitoCommand AtualizarCategorico(Guid id) =>
        new(id, "Parecer revisado", null, "CATEGORICO", null,
            [new ValorCategoricoCommand("INDEFERIDO", "Indeferido", false), new ValorCategoricoCommand("DEFERIDO", "Deferido", true), new ValorCategoricoCommand("EM_ANALISE", "Em análise", false)]);

    public static TipoConceito Numerico() =>
        TipoConceito.Criar("NOTA_ENTREVISTA", new DefinicaoInformada("Nota da entrevista", null, "NUMERICO", new EscalaInformada(0m, 10m, 2), null)).Value!;

    public static TipoConceito Categorico() =>
        TipoConceito.Criar("PARECER_ANALISE", new DefinicaoInformada(
            "Parecer da análise", null, "CATEGORICO", null,
            [new ValorInformado("DEFERIDO", "Deferido", true), new ValorInformado("INDEFERIDO", "Indeferido", false)])).Value!;
}
