namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Quem vê o pedido: as partes (o solicitante e o beneficiário, este também pelo e-mail verificado antes de aceitar) e
/// quem decide uma das etapas, administra concessões ou consulta os pedidos. Para os demais, o pedido não se distingue do
/// inexistente.
/// </summary>
internal static class VisibilidadeDoPedido
{
    /// <summary>
    /// Registra a negativa de quem pediu um pedido que não existe, para que a existência do pedido não apareça na
    /// falha do registro: quem é recusado tem a mesma resposta nos dois casos.
    /// </summary>
    public static Task RegistrarInexistenteAsync(IVerificadorDeAcesso verificador, CancellationToken cancellationToken) =>
        AcessoDoAprovador.RegistrarNegativasDoInexistenteAsync(verificador, FinalidadeDoAcesso.Leitura, cancellationToken);

    /// <summary>
    /// Confere a visibilidade, ou devolve o erro que a borda responde: 401 para a identidade incompleta, 404 para
    /// quem não vê. Quem não é parte tem a leitura registrada pela permissão que a deu ou, se nenhuma deu, pelas negativas.
    /// </summary>
    public static async Task<DomainError?> ConferirAsync(
        PedidoDeConcessao pedido, PessoaNoToken pessoa, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        if (pedido.EhParte(pessoa))
        {
            return null;
        }

        AcessoDoAprovador? acesso = await AcessoDoAprovador.ConsultarAsync(verificador, FinalidadeDoAcesso.Leitura, pedido.Id, cancellationToken).ConfigureAwait(false);
        if (acesso is null)
        {
            return PedidoHandlerErros.IdentidadeIncompleta();
        }

        await acesso.RegistrarDoDesfechoAsync(verificador, cancellationToken).ConfigureAwait(false);

        return acesso.VeTodos ? null : PedidoHandlerErros.NaoEncontrado();
    }
}
