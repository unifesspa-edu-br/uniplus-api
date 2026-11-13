namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Pedidos;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Quem vê o pedido: as partes (o solicitante e o beneficiário, este também pelo e-mail verificado antes de aceitar) e
/// quem decide uma das etapas, administra concessões ou consulta os pedidos. Para os demais, o pedido não se distingue do
/// inexistente. O detalhe, a trilha e o anexo passam todos por aqui.
/// </summary>
internal static class VisibilidadeDoPedido
{
    /// <summary>
    /// O pedido que a pessoa vê, com a leitura registrada, ou a recusa que a borda responde. O pedido inexistente tem a
    /// mesma resposta do pedido que a pessoa não vê: 404 a quem o token identifica, com a negativa registrada, e 401 a quem
    /// não identifica, como no pedido existente. Assim nem a resposta nem a falha do registro revelam se o pedido existe.
    /// </summary>
    public static async Task<Result<PedidoDeConcessao>> ObterAsync(
        Guid id, PessoaNoToken pessoa, IPedidoDeConcessaoRepository repository, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);

        PedidoDeConcessao? pedido = await repository.ObterParaLeituraAsync(id, cancellationToken).ConfigureAwait(false);
        if (pedido is null)
        {
            return Result<PedidoDeConcessao>.Failure(await RecusaDoInexistenteAsync(verificador, cancellationToken).ConfigureAwait(false));
        }

        DomainError? recusa = await ConferirAsync(pedido, pessoa, verificador, cancellationToken).ConfigureAwait(false);

        return recusa is null ? Result<PedidoDeConcessao>.Success(pedido) : Result<PedidoDeConcessao>.Failure(recusa);
    }

    /// <summary>
    /// Confere a visibilidade, ou devolve o erro que a borda responde: 401 para a identidade incompleta, 404 para
    /// quem não vê. Quem é parte tem a leitura registrada pela consulta dos pedidos, quando a tem, ou por titularidade,
    /// quando não a tem (ADR-0166); quem não é parte, pela permissão que a deu ou, se nenhuma deu, pelas negativas. Sem o
    /// registro gravado, a leitura não sai.
    /// </summary>
    public static async Task<DomainError?> ConferirAsync(
        PedidoDeConcessao pedido, PessoaNoToken pessoa, IVerificadorDeAcesso verificador, CancellationToken cancellationToken) =>
        pedido.EhParte(pessoa)
            ? await ConferirParteAsync(pedido, verificador, cancellationToken).ConfigureAwait(false)
            : await ConferirQuemNaoEParteAsync(pedido, verificador, cancellationToken).ConfigureAwait(false);

    private static async Task<DomainError> RecusaDoInexistenteAsync(IVerificadorDeAcesso verificador, CancellationToken cancellationToken) =>
        await AcessoDoAprovador.RegistrarNegativasDoInexistenteAsync(verificador, FinalidadeDoAcesso.Leitura, cancellationToken).ConfigureAwait(false)
        ?? PedidoHandlerErros.NaoEncontrado();

    private static async Task<DomainError?> ConferirParteAsync(
        PedidoDeConcessao pedido, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        Result<DecisaoPermitida?> governante = await AcessoDoAprovador
            .GovernanteDaLeituraDaParteAsync(verificador, pedido.Id, cancellationToken).ConfigureAwait(false);

        return governante.IsFailure
            ? governante.Error
            : await AcessoDoAprovador.RegistrarLeituraAsync(verificador, governante.Value, [pedido.Id], cancellationToken).ConfigureAwait(false);
    }

    private static async Task<DomainError?> ConferirQuemNaoEParteAsync(
        PedidoDeConcessao pedido, IVerificadorDeAcesso verificador, CancellationToken cancellationToken)
    {
        AcessoDoAprovador? acesso = await AcessoDoAprovador.ConsultarAsync(verificador, FinalidadeDoAcesso.Leitura, pedido.Id, cancellationToken).ConfigureAwait(false);
        if (acesso is null)
        {
            return PedidoHandlerErros.IdentidadeIncompleta();
        }

        await acesso.RegistrarDoDesfechoAsync(verificador, cancellationToken).ConfigureAwait(false);

        return acesso.VeTodos ? null : PedidoHandlerErros.NaoEncontrado();
    }
}
