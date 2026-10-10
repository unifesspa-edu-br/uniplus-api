namespace Unifesspa.UniPlus.Configuracao.Application.Commands.RecursosAcessibilidade;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarRecursoAcessibilidadeCommand"/>. Valida código, nome
/// e descrição (422, sem I/O) ANTES de buscar o recurso por Id — validação sempre vence
/// 404. Como código e nome são editáveis, confere a unicidade de cada um entre recursos
/// vivos quando ele muda (ignorando o próprio registro) e protege a corrida traduzindo
/// a violação do índice único parcial no conflito da constraint violada.
/// </summary>
public static class AtualizarRecursoAcessibilidadeCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarRecursoAcessibilidadeCommand command,
        IRecursoAcessibilidadeRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<(CodigoRecursoAcessibilidade Codigo, string Nome, string? Descricao)> campos =
            RecursoAcessibilidade.ValidarCamposEditaveis(command.Codigo, command.Nome, command.Descricao);
        if (campos.IsFailure)
        {
            return Result.ValidationFailure(campos.Errors);
        }

        RecursoAcessibilidade? recurso = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (recurso is null)
        {
            return Result.Failure(new DomainError(
                RecursoAcessibilidadeErrorCodes.NaoEncontrado,
                "Recurso de acessibilidade não encontrado."));
        }

        // Código e nome são case-sensitive (Ordinal) — só checa colisão quando o valor
        // normalizado efetivamente muda em relação ao atual.
        if (!string.Equals(campos.Value.Codigo.Valor, recurso.Codigo.Valor, StringComparison.Ordinal)
            && await repository.CodigoExisteEntreVivosAsync(campos.Value.Codigo.Valor, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(UniqueConstraintViolation.CodigoJaExisteErro());
        }

        if (!string.Equals(campos.Value.Nome, recurso.Nome, StringComparison.Ordinal)
            && await repository.NomeExisteEntreVivosAsync(campos.Value.Nome, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(UniqueConstraintViolation.NomeJaExisteErro());
        }

        Result atualizarResult = recurso.Atualizar(command.Codigo, command.Nome, command.Descricao);
        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.ConflitoDeUnicidade(ex) is { } conflito)
        {
            // Corrida entre a checagem de unicidade e o UPDATE: o índice único parcial
            // dispara 23505 e viramos o mesmo conflito do caminho não-race.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(conflito);
        }

        return Result.Success();
    }
}
