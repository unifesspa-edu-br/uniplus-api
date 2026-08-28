namespace Unifesspa.UniPlus.Configuracao.Application.Commands.OfertasCurso;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarOfertaCursoCommand"/>. Só toca os atributos
/// regulatórios editáveis — curso, local e unidade ofertante são imutáveis
/// (o agregado nem expõe a mutação). Valida os campos do payload (422, sem I/O)
/// ANTES de buscar a oferta por Id — validação sempre vence 404. O guard da base
/// legal condicional ao programa é revalidado pelo agregado na transição.
/// </summary>
public static class AtualizarOfertaCursoCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarOfertaCursoCommand command,
        IOfertaCursoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result preCheck = OfertaCurso.ValidarCamposDoPayload(
            command.ProgramaDeOferta,
            command.FormatoPedagogico,
            command.RegimeDeFuncionamento,
            command.RegimeDeTurno,
            command.Turnos,
            command.EMecCodigo,
            command.CodigoSga,
            command.VagasAnuaisAutorizadas,
            command.BaseLegal,
            command.AtoAutorizacaoMec);

        if (preCheck.IsFailure)
        {
            return Result.ValidationFailure(preCheck.Errors);
        }

        OfertaCurso? oferta = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (oferta is null)
        {
            return Result.Failure(new DomainError(
                OfertaCursoErrorCodes.NaoEncontrada,
                "Oferta de curso não encontrada."));
        }

        Result atualizarResult = oferta.Atualizar(
            command.ProgramaDeOferta,
            command.FormatoPedagogico,
            command.RegimeDeFuncionamento,
            command.RegimeDeTurno,
            command.Turnos,
            command.EMecCodigo,
            command.CodigoSga,
            command.VagasAnuaisAutorizadas,
            command.BaseLegal,
            command.AtoAutorizacaoMec);

        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
