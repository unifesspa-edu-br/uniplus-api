namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Forma pela qual a regra de comprovação deve ser cumprida.</summary>
public enum FormaDeComprovacao
{
    [CodigoDeContrato("DOCUMENTO", "Documento")]
    Documento = 1,

    [CodigoDeContrato("TODOS_OS_ITENS", "Todos estes itens")]
    TodosOsItens = 2,

    [CodigoDeContrato("QUANTIDADE_MINIMA_DE_ITENS", "Pelo menos a quantidade indicada destes itens")]
    QuantidadeMinimaDeItens = 3,
}
