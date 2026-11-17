namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

/// <summary>As causas estruturais da conformidade do processo do teste: um item pendente do painel, uma causa.</summary>
internal static class CausasDeConformidadeDeTeste
{
    public static IReadOnlyList<FieldError> De(ProcessoSeletivo processo) =>
        ChecklistDeConformidade.CausasDeConformidade(processo.Declaracao());
}
