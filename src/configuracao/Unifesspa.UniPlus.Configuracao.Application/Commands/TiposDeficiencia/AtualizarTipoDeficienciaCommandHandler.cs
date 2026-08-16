namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarTipoDeficienciaCommand"/>. Valida nome e
/// descrição primeiro (sem I/O) — validação sempre vence 404 — só então busca
/// o registro por Id; como o nome é editável, confere a unicidade entre tipos
/// vivos quando ele muda (ignorando o próprio registro) e protege a corrida
/// traduzindo a violação do índice único parcial em <c>NomeJaExiste</c>.
/// </summary>
public static class AtualizarTipoDeficienciaCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarTipoDeficienciaCommand command,
        ITipoDeficienciaRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<(string Nome, string Descricao)> campos =
            TipoDeficiencia.ValidarCamposEditaveis(command.Nome, command.Descricao);
        if (campos.IsFailure)
        {
            return Result.ValidationFailure(campos.Errors);
        }

        TipoDeficiencia? tipo = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return Result.Failure(new DomainError(
                TipoDeficienciaErrorCodes.NaoEncontrado,
                "Tipo de deficiência não encontrado."));
        }

        // Nome é case-sensitive (Ordinal) — só checa colisão quando o nome
        // normalizado efetivamente muda em relação ao atual.
        if (!string.Equals(campos.Value.Nome, tipo.Nome, StringComparison.Ordinal)
            && await repository.NomeExisteEntreVivosAsync(campos.Value.Nome, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(NomeJaExisteErro());
        }

        Result atualizarResult = tipo.Atualizar(command.Nome, command.Descricao, command.Permanente);
        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsNomeConflict(constraint))
        {
            // Corrida entre a checagem de unicidade e o UPDATE: o índice único parcial
            // dispara 23505; sem descartar, o SaveChangesAsync automático do Wolverine
            // repetiria o mesmo UPDATE fora deste catch e o 409 pretendido viraria 500.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(NomeJaExisteErro());
        }

        return Result.Success();
    }

    private static DomainError NomeJaExisteErro() =>
        new(TipoDeficienciaErrorCodes.NomeJaExiste,
            "Já existe um tipo de deficiência vivo com o nome informado.");
}
