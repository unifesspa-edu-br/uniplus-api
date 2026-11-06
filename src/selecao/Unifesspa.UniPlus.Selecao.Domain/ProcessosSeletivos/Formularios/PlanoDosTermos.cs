namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

/// <summary>Os termos que substituem os da finalidade, já vinculados a ela.</summary>
internal sealed record PlanoDosTermos(FinalidadeFormulario Finalidade, IReadOnlyList<TermoExigidoFormulario> Termos)
{
    internal void Aplicar(ColecoesDoProcesso colecoes, Guid processoId)
    {
        colecoes.TermosExigidos.RemoveAll(t => t.Finalidade == Finalidade);
        foreach (TermoExigidoFormulario termo in Termos)
        {
            ((IParteDoProcesso)termo).VincularProcesso(processoId);
            termo.VincularFinalidade(Finalidade);
            colecoes.TermosExigidos.Add(termo);
        }
    }
}
