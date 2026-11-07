namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

/// <summary>A definição do formulário de uma finalidade: troca o conteúdo do existente ou cria o novo, com os itens iniciais.</summary>
internal sealed record PlanoDoFormulario(FormularioProcesso Novo, FormularioProcesso? Existente, MudancaDaConfiguracao Mudanca) : IPlanoDaEdicao
{
    public void AtualizarPartes() => Existente?.Substituir(Novo);
}
