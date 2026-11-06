namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

/// <summary>As duas coleções que a troca da árvore de exigências reescreve: as folhas e os nós.</summary>
internal sealed record PlanoDasExigencias(
    IReadOnlyList<NoExigencia> Raizes,
    IReadOnlyList<NoExigencia> TodosOsNos,
    IReadOnlyList<DocumentoExigido> Folhas)
{
    internal void Aplicar(ColecoesDoProcesso colecoes, Guid processoId)
    {
        PartesDoProcesso.Substituir(colecoes.DocumentosExigidos, Folhas, processoId);

        colecoes.NosExigencia.Clear();
        foreach (NoExigencia raiz in Raizes)
        {
            ((IParteDoProcesso)raiz).VincularProcesso(processoId);
        }

        colecoes.NosExigencia.AddRange(TodosOsNos);
    }
}
