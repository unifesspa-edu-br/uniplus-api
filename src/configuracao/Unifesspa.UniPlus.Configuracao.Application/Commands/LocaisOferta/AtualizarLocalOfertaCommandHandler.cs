namespace Unifesspa.UniPlus.Configuracao.Application.Commands.LocaisOferta;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarLocalOfertaCommand"/>. Valida antes de I/O só
/// o que é determinável sem o registro persistido — tipo, cidade, formato e
/// coerência do endereço com a cidade do próprio payload, código e-MEC — e só
/// então busca o Local de Oferta por Id (validação sempre vence 404). A
/// resolução final do endereço (preservando o instante do display cache quando
/// o conteúdo não muda) só é possível depois do fetch, mas o formato já foi
/// confirmado válido no pré-check com o mesmo payload e o mesmo instante — não
/// pode falhar de novo.
/// </summary>
public static class AtualizarLocalOfertaCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarLocalOfertaCommand command,
        ILocalOfertaRepository repository,
        ICampusRepository campusRepository,
        IConfiguracaoUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(campusRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        DateTimeOffset agora = timeProvider.GetUtcNow();

        (DomainError? enderecoErroPreCheck, ReferenciaEnderecoGeo? enderecoPreCheck) =
            EnderecoGeoInputMapping.Resolver(command.Endereco, existente: null, agora);

        Result validacaoPreCheck = LocalOferta.ValidarCampos(
            command.Tipo,
            command.CidadeCodigoIbge,
            command.CidadeNome,
            command.CidadeUf,
            enderecoErroPreCheck is null ? enderecoPreCheck : null,
            command.CodigoEmec);

        List<FieldError> errosPreCheck = [.. validacaoPreCheck.Errors];
        if (enderecoErroPreCheck is not null)
        {
            errosPreCheck.Add(new FieldError("endereco", enderecoErroPreCheck));
        }

        if (errosPreCheck.Count > 0)
        {
            return Result.ValidationFailure(errosPreCheck);
        }

        LocalOferta? local = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (local is null)
        {
            return Result.Failure(new DomainError(
                LocalOfertaErrorCodes.NaoEncontrado,
                "Local de Oferta não encontrado."));
        }

        if (command.CampusResponsavelId.HasValue
            && command.CampusResponsavelId != local.CampusResponsavelId
            && !await campusRepository.ExisteVivoAsync(command.CampusResponsavelId.Value, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(new DomainError(
                LocalOfertaErrorCodes.CampusResponsavelNaoEncontrado,
                "O Campus responsável informado não foi encontrado."));
        }

        // Só recarimba a proveniência/frescura do display cache quando o trio de
        // cidade efetivamente muda (mesma semântica do Campus).
        bool cidadeMudou = CidadeReferenciaMudou(command, local);
        string? cidadeOrigem = cidadeMudou ? ReferenciaCidadeGeo.OrigemGeoApi : local.CidadeOrigem;
        DateTimeOffset? cidadeAtualizadoEm = cidadeMudou ? agora : local.CidadeDisplayAtualizadoEm;

        // Resolve de novo com o Endereco atual (para preservar o instante do
        // display cache quando o conteúdo não muda) — o formato já foi confirmado
        // válido no pré-check acima, com o mesmo payload e o mesmo `agora`, então
        // esta chamada não pode falhar por formato.
        (DomainError? enderecoErro, ReferenciaEnderecoGeo? endereco) =
            EnderecoGeoInputMapping.Resolver(command.Endereco, local.Endereco, agora);

        Result atualizarResult = local.Atualizar(
            command.Tipo,
            command.CampusResponsavelId,
            command.CidadeCodigoIbge,
            command.CidadeNome,
            command.CidadeUf,
            cidadeOrigem,
            cidadeAtualizadoEm,
            enderecoErro is null ? endereco : null,
            command.CodigoEmec);

        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static bool CidadeReferenciaMudou(AtualizarLocalOfertaCommand command, LocalOferta local) =>
        !string.Equals(command.CidadeCodigoIbge!.Trim(), local.CidadeCodigoIbge, StringComparison.Ordinal)
        || !string.Equals(command.CidadeNome!.Trim(), local.CidadeNome, StringComparison.Ordinal)
        || !string.Equals(command.CidadeUf!.Trim(), local.CidadeUf, StringComparison.OrdinalIgnoreCase);
}
