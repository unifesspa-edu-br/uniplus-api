namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O que a aplicação do modelo grava: o formulário novo no lugar do existente e a cópia dos itens, grupos, termos e derivações.</summary>
internal sealed record PlanoDaCopiaDoModelo(
    CopiaDeModeloDeFormulario Copia,
    FormularioProcesso Novo,
    FormularioProcesso? Existente,
    IReadOnlySet<string> FatosDaCopia)
{
    internal void Aplicar(ColecoesDoProcesso colecoes, Guid processoId)
    {
        FinalidadeFormulario finalidade = Copia.Finalidade;
        if (Existente is not null)
        {
            Existente.Substituir(Novo);
            Existente.RegistrarOrigem(Copia.ModeloId, Copia.ModeloCodigo);
        }
        else
        {
            ((IParteDoProcesso)Novo).VincularProcesso(processoId);
            colecoes.Formularios.Add(Novo);
        }

        colecoes.Campos.RemoveAll(f => f.Finalidade == finalidade || (f.GrupoColetadoId is null && FatosDaCopia.Contains(f.FatoCodigo)));
        PartesDoProcesso.Substituir(colecoes.Campos, static _ => false, Copia.Itens, processoId);
        new PlanoDaColeta(finalidade, [], Copia.Grupos).AplicarGrupos(colecoes, processoId);
        PartesDoProcesso.Substituir(colecoes.TermosExigidos, t => t.Finalidade == finalidade, Copia.Termos, processoId);
        PartesDoProcesso.Substituir(colecoes.RegrasDerivacao, static _ => false, Copia.DerivacoesNovas, processoId);
    }
}
