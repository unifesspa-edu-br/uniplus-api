namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using System.Collections.Frozen;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>O que o aviso de uma situação diz: a finalidade do envio, o assunto, a abertura e a situação por extenso.</summary>
internal sealed record TextoDoAviso(string Finalidade, string Assunto, string Abertura, string Situacao);

/// <summary>
/// Os textos fixos dos avisos do pedido, por situação. A situação que não está aqui não gera aviso. Nenhum texto leva
/// dado de pessoa, justificativa nem motivo.
/// </summary>
internal static class TextosDoAvisoDoPedido
{
    private static readonly FrozenDictionary<SituacaoDoPedido, TextoDoAviso> Textos = new Dictionary<SituacaoDoPedido, TextoDoAviso>
    {
        [SituacaoDoPedido.AguardandoBeneficiario] = new(
            "pedido.aceite_pendente",
            "Pedido de acesso aguarda o seu aceite",
            "Há um pedido de acesso em seu nome que aguarda o seu aceite.",
            "Aguardando o aceite do beneficiário"),
        [SituacaoDoPedido.AguardandoGestor] = new(
            "pedido.fila_do_gestor",
            "Pedido de acesso aguarda a análise do gestor de acessos",
            "Um pedido de acesso aguarda a análise de adequação funcional, que cabe ao gestor de acessos.",
            "Aguardando o gestor de acessos"),
        [SituacaoDoPedido.AguardandoProtecaoDeDados] = new(
            "pedido.fila_da_protecao_de_dados",
            "Pedido de acesso aguarda a análise da proteção de dados",
            "Um pedido de acesso aguarda a análise de adequação à proteção de dados.",
            "Aguardando a proteção de dados"),
        [SituacaoDoPedido.Aprovado] = new(
            "pedido.aprovado",
            "Pedido de acesso aprovado",
            "O pedido de acesso foi aprovado e o acesso já está concedido.",
            "Aprovado"),
        [SituacaoDoPedido.Recusado] = new(
            "pedido.recusado",
            "Pedido de acesso recusado",
            "O pedido de acesso foi recusado.",
            "Recusado"),
        [SituacaoDoPedido.Cancelado] = new(
            "pedido.cancelado",
            "Pedido de acesso cancelado",
            "O pedido de acesso em seu nome foi cancelado pelo solicitante.",
            "Cancelado"),
    }.ToFrozenDictionary();

    public static TextoDoAviso Da(SituacaoDoPedido situacao) =>
        Textos.GetValueOrDefault(situacao) ?? throw new InvalidOperationException($"A situação {situacao} não gera aviso.");
}
