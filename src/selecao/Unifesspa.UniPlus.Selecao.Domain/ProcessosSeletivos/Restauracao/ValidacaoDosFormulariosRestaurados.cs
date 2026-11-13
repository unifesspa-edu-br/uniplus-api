namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ValidacaoDosFormulariosRestaurados
{
    // Confere as finalidades que o envelope traz nos itens e nos grupos, tenha o processo o
    // formulário da finalidade ou não: a restauração não pode confiar na presença dele.
    internal static DomainError? ItensEmExcesso(GrafoConfiguracao grafo) =>
        FinalidadesComItens(grafo)
            .Select(finalidade => FormaDoItem.ValidarQuantidade(ItensNoTeto(grafo, finalidade)))
            .FirstOrDefault(static erros => erros.Count > 0) is [var excesso, ..]
            ? excesso.Error
            : null;

    private static IEnumerable<FinalidadeFormulario> FinalidadesComItens(GrafoConfiguracao grafo) =>
        grafo.FatosColetados.Select(static f => f.Finalidade)
            .Concat(grafo.GruposColetados.Select(static g => g.Finalidade))
            .Distinct()
            .Order();

    private static int ItensNoTeto(GrafoConfiguracao grafo, FinalidadeFormulario finalidade) =>
        CitacoesDosFormularios.QuantidadeNoTeto(
            grafo.FatosColetados.Count(f => f.Finalidade == finalidade),
            grafo.GruposColetados.Where(g => g.Finalidade == finalidade));
}
