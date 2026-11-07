namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O que a aplicação do modelo grava: o formulário novo no lugar do existente e a cópia dos itens, grupos, termos e derivações.</summary>
internal sealed record PlanoDaCopiaDoModelo(
    CopiaDeModeloDeFormulario Copia,
    FormularioProcesso Novo,
    FormularioProcesso? Existente,
    MudancaDaConfiguracao Mudanca) : IPlanoDaEdicao
{
    public void AtualizarPartes()
    {
        if (Existente is null)
        {
            return;
        }

        Existente.Substituir(Novo);
        Existente.RegistrarOrigem(Copia.ModeloId, Copia.ModeloCodigo);
    }
}
