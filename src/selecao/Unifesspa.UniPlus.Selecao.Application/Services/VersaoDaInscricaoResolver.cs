namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O único ponto que diz em que versão do formulário a inscrição está (UNI-REQ-0084): o rascunho segue a
/// versão divulgada do certame, e a inscrição enviada fica na que o primeiro envio congelou. Nunca o
/// relógio: a divulgação aponta o ato que confirmou a publicidade, então uma retificação sem publicidade
/// não é adotada. Salvar, enviar, avaliar, ler a inscrição e ler o formulário partem daqui.
/// </summary>
public static class VersaoDaInscricaoResolver
{
    public static async Task<Result<VersaoConfiguracao>> ResolverAsync(
        Inscricao inscricao,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);

        if (inscricao.Situacao == SituacaoInscricao.Submetida)
        {
            return await VersaoFixadaDaInscricaoResolver
                .ResolverAsync(inscricao.VersaoFormularioId, inscricao.ProcessoSeletivoId, processoSeletivoRepository, cancellationToken)
                .ConfigureAwait(false);
        }

        Result<VersaoDivulgada> divulgada = await VersaoDivulgadaResolver
            .ResolverAsync(inscricao.ProcessoSeletivoId, certameDivulgadoRepository, processoSeletivoRepository, cancellationToken)
            .ConfigureAwait(false);
        return divulgada.IsFailure
            ? Result<VersaoConfiguracao>.Failure(divulgada.Error!)
            : Result<VersaoConfiguracao>.Success(divulgada.Value!.Versao);
    }

    /// <summary>
    /// Leva o rascunho à versão que vale (<see cref="Inscricao.AdotarVersao"/>), na memória: quem grava é o
    /// salvar e o enviar, e a leitura só projeta o rascunho já adotado. A inscrição enviada fica como está.
    /// </summary>
    public static async Task<Result> AdotarAsync(
        Inscricao inscricao,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inscricao);

        if (inscricao.Situacao == SituacaoInscricao.Submetida)
        {
            return Result.Success();
        }

        Result<VersaoDivulgada> vigente = await VersaoDivulgadaResolver
            .ResolverAsync(inscricao.ProcessoSeletivoId, certameDivulgadoRepository, processoSeletivoRepository, cancellationToken)
            .ConfigureAwait(false);
        if (vigente.IsFailure)
        {
            return Result.Failure(vigente.Error!);
        }

        VersaoConfiguracao versao = vigente.Value!.Versao;
        if (versao.Id == inscricao.VersaoFormularioId)
        {
            return Result.Success();
        }

        Result<EnvelopeReidratado> reidratado = ContextoDaVersaoFixadaResolver.Reidratar(versao, registroCodecs);
        return reidratado.IsFailure
            ? Result.Failure(reidratado.Error!)
            : inscricao.AdotarVersao(versao.Id, ContextoDaVersaoFixadaResolver.Admitidas(reidratado.Value!.Grafo));
    }
}
