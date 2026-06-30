namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CondicoesAtendimento;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarCondicaoAtendimentoCommand"/>. Como o código é
/// editável, confere a unicidade entre condições vivas quando ele muda (ignorando
/// o próprio registro) e protege a corrida traduzindo a violação do índice único
/// parcial em <c>CodigoJaExiste</c>. O bloqueio de renomeação do código reservado
/// <c>PCD</c> é aplicado pelo agregado em <c>Atualizar</c>
/// (<c>CodigoProtegidoNaoEditavel</c>).
/// </summary>
public static class AtualizarCondicaoAtendimentoCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarCondicaoAtendimentoCommand command,
        ICondicaoAtendimentoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        CondicaoAtendimentoEspecializado? condicao = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (condicao is null)
        {
            return Result.Failure(new DomainError(
                CondicaoAtendimentoErrorCodes.NaoEncontrada,
                "Condição de atendimento especializado não encontrada."));
        }

        // Código é case-sensitive (Ordinal), normalizado por Trim no agregado — só
        // checa colisão quando o código efetivamente muda.
        if (!string.Equals(command.Codigo.Trim(), condicao.Codigo.Valor, StringComparison.Ordinal)
            && await repository.CodigoExisteEntreVivosAsync(command.Codigo, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(CodigoJaExisteErro(command.Codigo));
        }

        Result atualizarResult = condicao.Atualizar(
            command.Codigo,
            command.Nome,
            command.Descricao);

        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsCodigoConflict(constraint))
        {
            // Corrida entre a checagem de unicidade e o UPDATE: o índice único parcial
            // dispara 23505 e viramos o mesmo CodigoJaExiste do caminho não-race.
            return Result.Failure(CodigoJaExisteErro(command.Codigo));
        }

        return Result.Success();
    }

    private static DomainError CodigoJaExisteErro(string codigo) =>
        new(CondicaoAtendimentoErrorCodes.CodigoJaExiste,
            $"Já existe uma condição de atendimento especializado viva com o código '{codigo}'.");
}
