namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os formulários do grafo restaurado: o teto de itens por finalidade, o mesmo da gravação. Sem o espelho, a
/// restauração aplicaria uma lista que o PUT seguinte da mesma lista recusaria.
/// </summary>
internal static class ValidacaoDosFormulariosRestaurados
{
    internal static IReadOnlyList<FieldError> Validar(GrafoConfiguracao grafo) =>
        CausasDaRecusa.NoCampo(
            grafo.FatosColetados.Select(static f => f.Finalidade).Concat(grafo.GruposColetados.Select(static g => g.Finalidade)).Distinct().Order()
                .SelectMany(finalidade => FormaDoItem.ValidarQuantidade(CitacoesDosFormularios.QuantidadeNoTeto(
                    grafo.FatosColetados.Count(f => f.Finalidade == finalidade), grafo.GruposColetados.Where(g => g.Finalidade == finalidade)))),
            "formularios");
}
