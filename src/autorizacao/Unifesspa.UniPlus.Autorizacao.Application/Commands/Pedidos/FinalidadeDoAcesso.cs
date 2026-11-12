namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

/// <summary>
/// Para que se consulta o acesso ao fluxo dos pedidos, o que define quais permissões entram na avaliação: a decisão de
/// uma etapa só avalia as permissões de decidir, para que aprovar e recusar nunca registrem uma permissão que não governou.
/// </summary>
internal enum FinalidadeDoAcesso
{
    /// <summary>Aprovar ou recusar a etapa que o pedido aguarda.</summary>
    Decisao = 0,

    /// <summary>Listar uma fila: quem decide alguma etapa ou quem consulta os pedidos.</summary>
    Fila,

    /// <summary>Ler um pedido alheio: quem decide, quem administra concessões ou quem consulta os pedidos.</summary>
    Leitura,
}
