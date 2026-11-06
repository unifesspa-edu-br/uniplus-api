namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

/// <summary>O que gravar quando a coleta de uma finalidade é trocada: os itens e os grupos com os campos deles.</summary>
internal sealed record PlanoDaColeta(
    FinalidadeFormulario Finalidade,
    IReadOnlyList<FatoColetado> Fatos,
    IReadOnlyList<GrupoColetado> Grupos)
{
    internal void Aplicar(ColecoesDoProcesso colecoes, Guid processoId)
    {
        // Sai tudo o que o formulário coletava, itens e campos dos grupos, e entra o novo.
        colecoes.Campos.RemoveAll(f => f.Finalidade == Finalidade);
        foreach (FatoColetado fato in Fatos)
        {
            ((IParteDoProcesso)fato).VincularProcesso(processoId);
            fato.VincularFinalidade(Finalidade);
            colecoes.Campos.Add(fato);
        }

        AplicarGrupos(colecoes, processoId);
    }

    internal void AplicarGrupos(ColecoesDoProcesso colecoes, Guid processoId)
    {
        colecoes.GruposColetados.RemoveAll(g => g.Finalidade == Finalidade);
        foreach (GrupoColetado grupo in Grupos)
        {
            ((IParteDoProcesso)grupo).VincularProcesso(processoId);
            grupo.VincularFinalidade(Finalidade);
            colecoes.GruposColetados.Add(grupo);
            colecoes.Campos.AddRange(grupo.Subitens);
        }
    }
}
