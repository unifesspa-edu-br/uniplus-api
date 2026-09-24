namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Valida os campos sem I/O antes de buscar o tipo: a violação de campo vence o "não
/// encontrado", e um payload mal formado não consulta o repositório (ADR-0125, item 5). Por
/// isso a recusa que depende do tipo só aparece quando os campos estão válidos.
/// </summary>
public static class AtualizarTipoEtapaCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarTipoEtapaCommand command,
        ITipoEtapaRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<(string Nome, string? Descricao, bool AdmitePontuacao, bool AdmiteEliminacao)> campos =
            TipoEtapa.ValidarCamposEditaveis(
                command.Nome, command.Descricao, command.AdmitePontuacao, command.AdmiteEliminacao);
        if (campos.IsFailure)
        {
            return Result.ValidationFailure(campos.Errors);
        }

        TipoEtapa? tipo = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return Result.Failure(new DomainError(TipoEtapaErrorCodes.NaoEncontrado, "Tipo de etapa não encontrado."));
        }

        // Com os campos já confirmados, só a regra que depende do próprio tipo pode recusar
        // aqui, e o tipo recusa antes de mutar.
        Result atualizar = tipo.Atualizar(command.Nome, command.Descricao, command.AdmitePontuacao, command.AdmiteEliminacao);
        if (atualizar.IsFailure)
        {
            return atualizar;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }
}
