namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Providencias;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Os handlers dos atos da providência. Quem age é o sujeito autenticado: sem emissor e sub nada é gravado, e a recusa não
/// vira 500. Os casos de uso vivem em <see cref="AberturaDaProvidencia"/>, <see cref="RespostaDaProvidencia"/> e
/// <see cref="AtendimentoDaProvidencia"/>; os avisos saem como mensagens de saída, no outbox da mesma transação.
/// </summary>
public static class ProvidenciaCommandHandler
{
    public static async Task<(Result<ProvidenciaDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        SolicitarProvidenciaCommand command, AberturaDaProvidencia abertura, IUserContext userContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(abertura);
        ArgumentNullException.ThrowIfNull(userContext);

        return IdentidadeDoToken.PessoaDe(userContext) is { } solicitante
            ? await abertura.AbrirAsync(command, solicitante, cancellationToken).ConfigureAwait(false)
            : AutorNaoIdentificado();
    }

    public static async Task<(Result<ProvidenciaDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        ResponderProvidenciaCommand command, RespostaDaProvidencia resposta, IUserContext userContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(resposta);
        ArgumentNullException.ThrowIfNull(userContext);

        return IdentidadeDoToken.De(userContext) is { } quemResponde
            ? await resposta.ResponderAsync(command, quemResponde, cancellationToken).ConfigureAwait(false)
            : AutorNaoIdentificado();
    }

    public static async Task<(Result<ProvidenciaDto> Resposta, IEnumerable<object> Mensagens)> Handle(
        AtenderProvidenciaCommand command, AtendimentoDaProvidencia atendimento, IUserContext userContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(atendimento);
        ArgumentNullException.ThrowIfNull(userContext);

        return IdentidadeDoToken.De(userContext) is { } quemAtende
            ? await atendimento.AtenderAsync(command, quemAtende, cancellationToken).ConfigureAwait(false)
            : AutorNaoIdentificado();
    }

    private static (Result<ProvidenciaDto> Resposta, IEnumerable<object> Mensagens) AutorNaoIdentificado() =>
        (Result<ProvidenciaDto>.Failure(ProvidenciaHandlerErros.AutorNaoIdentificado()), []);
}
