namespace Unifesspa.UniPlus.Configuracao.Application.Commands.LocaisOferta;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarLocalOfertaCommand"/>. Confere antes de I/O só
/// o que é determinável sem o registro persistido — tipo, cidade, formato e
/// coerência do endereço com a cidade da própria declaração, código e-MEC — e só
/// então busca o Local de Oferta por Id (validação sempre vence 404). A
/// resolução final do endereço (preservando o instante do display cache quando
/// o conteúdo não muda) só é possível depois do fetch, mas o formato já foi
/// confirmado válido na conferência prévia com a mesma declaração e o mesmo
/// instante — não pode falhar de novo.
/// </summary>
public static class AtualizarLocalOfertaCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarLocalOfertaCommand command,
        ILocalOfertaRepository repository,
        ICampusRepository campusRepository,
        IUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(campusRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        DateTimeOffset agora = timeProvider.GetUtcNow();
        Result declaracao = ConferirDeclaracao(command, agora);

        return declaracao.IsFailure
            ? declaracao
            : await AtualizarAsync(command, repository, campusRepository, unitOfWork, agora, cancellationToken).ConfigureAwait(false);
    }

    private static Result ConferirDeclaracao(AtualizarLocalOfertaCommand command, DateTimeOffset agora)
    {
        (DomainError? enderecoErro, ReferenciaEnderecoGeo? endereco) =
            EnderecoGeoInputMapping.Resolver(command.Endereco, existente: null, agora);

        Result declaracao = LocalOferta.ValidarDeclaracao(command.Declaracao(enderecoErro is null ? endereco : null));
        List<FieldError> erros = [.. declaracao.Errors];
        if (enderecoErro is not null)
        {
            erros.Add(new FieldError("endereco", enderecoErro));
        }

        return erros.Count > 0 ? Result.ValidationFailure(erros) : Result.Success();
    }

    private static async Task<Result> AtualizarAsync(
        AtualizarLocalOfertaCommand command,
        ILocalOfertaRepository repository,
        ICampusRepository campusRepository,
        IUnitOfWork unitOfWork,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        LocalOferta? local = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (local is null)
        {
            return Result.Failure(new DomainError(LocalOfertaErrorCodes.NaoEncontrado, "Local de Oferta não encontrado."));
        }

        Result campus = await ConferirCampusResponsavelAsync(command, local, campusRepository, cancellationToken).ConfigureAwait(false);

        return campus.IsFailure
            ? campus
            : await GravarAsync(command, local, unitOfWork, agora, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result> GravarAsync(
        AtualizarLocalOfertaCommand command, LocalOferta local, IUnitOfWork unitOfWork, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        Result atualizado = local.Atualizar(command.Declaracao(EnderecoPreservandoOInstante(command, local, agora)), agora);
        if (atualizado.IsFailure)
        {
            return atualizado;
        }

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static async Task<Result> ConferirCampusResponsavelAsync(
        AtualizarLocalOfertaCommand command, LocalOferta local, ICampusRepository campusRepository, CancellationToken cancellationToken)
    {
        bool campusMudou = command.CampusResponsavelId.HasValue && command.CampusResponsavelId != local.CampusResponsavelId;

        return campusMudou
            ? await CampusResponsavelDoLocal.ConferirAsync(command.CampusResponsavelId!.Value, campusRepository, cancellationToken).ConfigureAwait(false)
            : Result.Success();
    }

    // O formato do endereço já foi confirmado na conferência prévia, com a mesma declaração e o mesmo instante: esta resolução
    // só existe para preservar o instante do display cache quando o conteúdo do endereço não muda.
    private static ReferenciaEnderecoGeo? EnderecoPreservandoOInstante(
        AtualizarLocalOfertaCommand command, LocalOferta local, DateTimeOffset agora)
    {
        (DomainError? enderecoErro, ReferenciaEnderecoGeo? endereco) =
            EnderecoGeoInputMapping.Resolver(command.Endereco, local.Endereco, agora);

        return enderecoErro is null ? endereco : null;
    }
}
