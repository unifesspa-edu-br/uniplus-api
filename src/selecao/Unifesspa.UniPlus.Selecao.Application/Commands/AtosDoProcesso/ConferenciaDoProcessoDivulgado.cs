namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O órgão que numera o ato e a divulgação do certame, conferidos antes de qualquer escrita.</summary>
/// <param name="Orgao">Sigla da unidade administradora do processo.</param>
/// <param name="Divulgado">
/// A divulgação do certame, rastreada: de onde vêm a unidade e o identificador da chave no acervo, e onde o ato
/// publicado registra a entrada que a cópia ao acervo preencherá.
/// </param>
internal sealed record ProcessoDivulgadoConferido(string Orgao, CertameDivulgado Divulgado);

/// <summary>Confere a existência do processo e carrega sua divulgação antes da publicação de um ato.</summary>
public sealed class ConferenciaDoProcessoDivulgado(
    IProcessoSeletivoRepository processoSeletivoRepository,
    ICertameDivulgadoRepository certameDivulgadoRepository)
{
    internal async Task<Result<ProcessoDivulgadoConferido>> ConferirAsync(
        Guid processoSeletivoId,
        CancellationToken cancellationToken)
    {
        string? orgao = await processoSeletivoRepository
            .ObterSiglaDaUnidadeAdministradoraAsync(processoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (orgao is null)
        {
            return Falha("Processo seletivo não encontrado.", "ProcessoSeletivo.NaoEncontrado");
        }

        return await ConferirDivulgacaoAsync(processoSeletivoId, orgao, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<ProcessoDivulgadoConferido>> ConferirDivulgacaoAsync(
        Guid processoSeletivoId, string orgao, CancellationToken cancellationToken)
    {
        CertameDivulgado? divulgado = await certameDivulgadoRepository
            .ObterParaMutacaoAsync(processoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        return divulgado is null
            ? Falha(
                "O processo ainda não tem página pública: os atos do processo aparecem na linha do tempo do certame, que nasce com a publicação do edital de abertura.",
                AtoDoProcessoErrorCodes.CertameNaoDivulgado)
            : Result<ProcessoDivulgadoConferido>.Success(new ProcessoDivulgadoConferido(orgao, divulgado));
    }

    private static Result<ProcessoDivulgadoConferido> Falha(string mensagem, string codigo) =>
        Result<ProcessoDivulgadoConferido>.Failure(new DomainError(codigo, mensagem));
}
