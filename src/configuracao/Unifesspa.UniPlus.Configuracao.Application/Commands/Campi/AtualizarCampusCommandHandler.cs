namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

public static class AtualizarCampusCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarCampusCommand command,
        ICampusRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        Campus? campus = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (campus is null)
        {
            return Result.Failure(new DomainError(
                CampusErrorCodes.NaoEncontrado,
                "Campus não encontrado."));
        }

        // Sigla normaliza para uppercase no agregado e o índice único é case-insensitive
        // (comparação OrdinalIgnoreCase) — só checa colisão quando a sigla muda.
        if (!string.Equals(command.Sigla.Trim(), campus.Sigla, StringComparison.OrdinalIgnoreCase)
            && await repository.SiglaExisteEntreLivosAsync(command.Sigla, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(new DomainError(
                CampusErrorCodes.SiglaJaExiste,
                $"Já existe um Campus vivo com a sigla '{command.Sigla}'."));
        }

        DateTimeOffset agora = timeProvider.GetUtcNow();

        // Só recarimba a proveniência/frescura do display cache quando o trio de
        // cidade efetivamente muda — assim cidade_display_atualizado_em rastreia a
        // última reconciliação da cidade, não qualquer edição de outro campo.
        bool cidadeMudou = CidadeReferenciaMudou(command, campus);
        string? cidadeOrigem = cidadeMudou ? ReferenciaCidadeGeo.OrigemGeoApi : campus.CidadeOrigem;
        DateTimeOffset? cidadeAtualizadoEm = cidadeMudou ? agora : campus.CidadeDisplayAtualizadoEm;

        (DomainError? enderecoErro, ReferenciaEnderecoGeo? endereco) =
            EnderecoGeoInputMapping.Resolver(command.Endereco, campus.Endereco, agora);
        if (enderecoErro is not null)
        {
            return Result.Failure(enderecoErro);
        }

        Result atualizarResult = campus.Atualizar(
            command.Sigla,
            command.Nome,
            command.CidadeCodigoIbge,
            command.CidadeNome,
            command.CidadeUf,
            cidadeOrigem,
            cidadeAtualizadoEm,
            endereco,
            command.CodigoEmec);

        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static bool CidadeReferenciaMudou(AtualizarCampusCommand command, Campus campus) =>
        !string.Equals(command.CidadeCodigoIbge.Trim(), campus.CidadeCodigoIbge, StringComparison.Ordinal)
        || !string.Equals(command.CidadeNome.Trim(), campus.CidadeNome, StringComparison.Ordinal)
        || !string.Equals(command.CidadeUf.Trim(), campus.CidadeUf, StringComparison.OrdinalIgnoreCase);
}
