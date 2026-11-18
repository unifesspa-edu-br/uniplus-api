namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Recursos;

using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ProcedimentosDeRecursoParaTestes
{
    internal static readonly Guid Item = Guid.Parse("11111111-1111-1111-1111-111111111111");
    internal static readonly Guid OutroItem = Guid.Parse("22222222-2222-2222-2222-222222222222");

    internal static ReferenciaRegra Regra(string codigo = RegraProcedimentoRecursoCodigo.Recorrecao) =>
        ReferenciaRegra.De(codigo, "v1", new string('a', 64));

    internal static ProcedimentoDeRecursoDeclarado Procedimento(
        TipoMotivoDeDecisao tipo = TipoMotivoDeDecisao.Classificacao,
        AlvoDoProcedimentoDeRecurso? alvo = null,
        string codigoDaRegra = RegraProcedimentoRecursoCodigo.Recalculo) =>
        ProcedimentoDeRecursoDeclarado.De(tipo, alvo, Regra(codigoDaRegra));

    internal static ContextoDosProcedimentosDeRecurso Contexto(
        bool processoTemNotaDoEnem = false,
        TipoMotivoDeDecisao[]? tiposDoLugar = null,
        Guid[]? itens = null) =>
        new(
            (tiposDoLugar ?? [TipoMotivoDeDecisao.Classificacao]).ToHashSet(),
            (itens ?? [Item]).ToHashSet(),
            processoTemNotaDoEnem);

    internal static AlvoDoProcedimentoDeRecurso AlvoItem(Guid? id = null) => AlvoDoProcedimentoDeRecurso.DoItem(id ?? Item);

    internal static AlvoDoProcedimentoDeRecurso AlvoArea(string area = AreaDoEnem.Matematica) => AlvoDoProcedimentoDeRecurso.DaArea(area);
}
