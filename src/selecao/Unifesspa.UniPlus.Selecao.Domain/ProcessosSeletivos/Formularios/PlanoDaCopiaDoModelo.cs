namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>O que a aplicação do modelo grava: o conteúdo novo no formulário existente, que mantém a identidade, e a cópia dos itens, grupos, termos e derivações.</summary>
internal sealed record PlanoDaCopiaDoModelo(
    CopiaDeModeloDeFormulario Copia,
    FormularioProcesso Novo,
    FormularioProcesso Existente,
    MudancaDaConfiguracao Mudanca) : IPlanoDaEdicao
{
    public void AtualizarPartes()
    {
        Existente.Substituir(Novo);
        Existente.RegistrarOrigem(Copia.Origem);
    }
}
