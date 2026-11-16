namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Abstractions;

using Commands.Inscricoes;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;
using DTOs.Comprovantes;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Envia ou corrige a inscrição do candidato: a avaliação completa decide o veredito, nunca a gravação — ou tudo passa, ou nada é
/// persistido. A avaliação roda sobre o estado FINAL (o que já estava gravado, mesclado com o corpo recebido), porque o envio exige
/// os obrigatórios do formulário inteiro. A ordem é a das recusas: o preparo e a forma do corpo, a avaliação, as recusas juntas e,
/// só então, a gravação, que confere a precondição de versão.
/// </summary>
public sealed class EnvioDaInscricao(PreparoDoEnvio preparo, AvaliacaoDoEnvio avaliacao, GravacaoDoEnvio gravacao)
{
    public async Task<Result<ReciboDaInscricaoDto>> EnviarAsync(
        Inscricao inscricao, SubmeterInscricaoCommand command, IUserContext userContext, DateTimeOffset instante, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Result<PedidoDoEnvio> pedido = await preparo.PrepararAsync(inscricao, command.Request, userContext, cancellationToken).ConfigureAwait(false);
        return pedido.IsFailure
            ? Result<ReciboDaInscricaoDto>.ValidationFailure(pedido.Errors)
            : await AvaliarEGravarAsync(pedido.Value!, command.Precondicao, instante, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<ReciboDaInscricaoDto>> AvaliarEGravarAsync(
        PedidoDoEnvio pedido, PrecondicaoIfMatch precondicao, DateTimeOffset instante, CancellationToken cancellationToken)
    {
        Result<AvaliadoDoEnvio> avaliado = await avaliacao.AvaliarAsync(pedido, instante, cancellationToken).ConfigureAwait(false);
        Result recusas = avaliado.IsFailure ? Result.ValidationFailure(avaliado.Errors) : RecusasDoEnvio.Conferir(pedido, avaliado.Value!);
        return recusas.IsFailure
            ? Result<ReciboDaInscricaoDto>.ValidationFailure(recusas.Errors)
            : await gravacao.GravarAsync(pedido, avaliado.Value!, precondicao, cancellationToken).ConfigureAwait(false);
    }
}
