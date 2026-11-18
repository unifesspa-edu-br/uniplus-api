namespace Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;

using Services;

/// <summary>
/// Remove do armazenamento os objetos dos documentos a publicar de um rascunho excluído. Sai em cascata da exclusão,
/// agendada para depois de a URL de envio expirar e da tolerância da rotina dos pendentes vencidos: um <c>PUT</c>
/// pré-assinado ainda pode chegar até lá, e o objeto que ele gravasse ficaria sem registro. Leva só chaves de objeto, que
/// são identificadores, nunca o nome do arquivo.
/// </summary>
/// <param name="Chaves">As chaves de objeto a remover, duas por documento: a de envio e a da cópia gravada na confirmação, que pode existir mesmo no documento ainda pendente.</param>
public sealed record RemoverArquivosDoRascunhoExcluido(IReadOnlyList<string> Chaves)
{
    /// <summary>Quanto a mensagem espera depois da exclusão antes de ser entregue.</summary>
    public static TimeSpan Atraso { get; } = PrazosDoArquivoEnviado.Envio + RemocaoDeArquivosPendentesVencidos.Tolerancia;
}
