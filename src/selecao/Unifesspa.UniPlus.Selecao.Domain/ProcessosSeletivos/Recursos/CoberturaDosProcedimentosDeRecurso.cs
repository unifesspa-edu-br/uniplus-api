namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;

using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Quais tipos de decisão que o lugar declara ficam sem procedimento. Um procedimento sem alvo cobre o tipo inteiro;
/// procedimentos com alvo só cobrem o tipo quando juntos alcançam todo o universo do alvo: todos os itens de avaliação
/// da etapa, e todas as áreas do ENEM quando o processo importa a nota. Função pura sobre a declaração e o lugar.
/// </summary>
public static class CoberturaDosProcedimentosDeRecurso
{
    public static IReadOnlyList<TipoMotivoDeDecisao> TiposSemProcedimento(
        ProcedimentosDeRecurso declarados, ContextoDosProcedimentosDeRecurso contexto)
    {
        ArgumentNullException.ThrowIfNull(declarados);
        ArgumentNullException.ThrowIfNull(contexto);

        return [.. ((IEnumerable<TipoMotivoDeDecisao>?)contexto.TiposDeDecisaoDoLugar ?? [])
            .Where(tipo => !TipoCoberto(declarados.Itens.Where(p => p.TipoDeDecisao == tipo).ToList(), contexto))
            .Order()];
    }

    private static bool TipoCoberto(List<ProcedimentoDeRecursoDeclarado> doTipo, ContextoDosProcedimentosDeRecurso contexto) =>
        doTipo.Any(static p => p.Alvo is null)
        || (doTipo.Count > 0 && UniversoCoberto(doTipo, TipoDeAlvoDoProcedimentoDeRecurso.ItemDeAvaliacao, ItensDoUniverso(contexto))
            && UniversoCoberto(doTipo, TipoDeAlvoDoProcedimentoDeRecurso.AreaDoEnem, AreasDoUniverso(contexto)));

    private static HashSet<string> ItensDoUniverso(ContextoDosProcedimentosDeRecurso contexto) =>
        [.. contexto.ItensDeAvaliacao.Select(static id => id.ToString("D"))];

    private static HashSet<string> AreasDoUniverso(ContextoDosProcedimentosDeRecurso contexto) =>
        contexto.ProcessoTemNotaDoEnem ? [.. AreaDoEnem.Todas] : [];

    // O universo só conta quando o tipo declara algum alvo daquele gênero: quem declara só itens não precisa cobrir áreas.
    private static bool UniversoCoberto(
        List<ProcedimentoDeRecursoDeclarado> doTipo, TipoDeAlvoDoProcedimentoDeRecurso genero, HashSet<string> universo)
    {
        HashSet<string> alvos = [.. doTipo.Where(p => p.Alvo?.Tipo == genero).Select(static p => p.Alvo!.Valor)];
        return alvos.Count == 0 || universo.IsSubsetOf(alvos);
    }
}
