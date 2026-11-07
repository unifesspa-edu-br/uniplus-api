namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

/// <summary>
/// O plano de uma edição que, além de mudar as coleções, atualiza no lugar partes que continuam no
/// processo. A raiz aplica as duas coisas, as partes primeiro.
/// </summary>
internal interface IPlanoDaEdicao
{
    MudancaDaConfiguracao Mudanca { get; }

    void AtualizarPartes();
}
