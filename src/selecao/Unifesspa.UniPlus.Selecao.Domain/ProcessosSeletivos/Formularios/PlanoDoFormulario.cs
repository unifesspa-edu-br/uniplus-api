namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

/// <summary>A definição do formulário de uma finalidade: troca o existente ou cria o novo, com os itens iniciais.</summary>
internal sealed record PlanoDoFormulario(
    FinalidadeFormulario Finalidade,
    FormularioProcesso Novo,
    FormularioProcesso? Existente,
    IReadOnlyList<FatoColetado>? ItensIniciais)
{
    internal void Aplicar(ColecoesDoProcesso colecoes, Guid processoId)
    {
        if (Existente is not null)
        {
            Existente.Substituir(Novo);
            return;
        }

        ((IParteDoProcesso)Novo).VincularProcesso(processoId);
        colecoes.Formularios.Add(Novo);
        if (ItensIniciais is not null)
        {
            new PlanoDaColeta(Finalidade, ItensIniciais, []).Aplicar(colecoes, processoId);
        }
    }
}
