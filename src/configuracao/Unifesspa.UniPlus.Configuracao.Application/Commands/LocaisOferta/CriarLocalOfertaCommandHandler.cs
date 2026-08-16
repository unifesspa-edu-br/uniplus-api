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
/// Handler do <see cref="CriarLocalOfertaCommand"/>: valida o agregado por
/// inteiro primeiro (sem I/O) — tipo, cidade, coerência com o endereço e código
/// e-MEC acumulados no mesmo lote — só então confere a existência do campus
/// responsável (quando informado, FK intra-banco opcional ADR-0065), cria o
/// agregado carimbando a proveniência do display cache e persiste.
/// </summary>
public static class CriarLocalOfertaCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarLocalOfertaCommand command,
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

        (DomainError? enderecoErro, ReferenciaEnderecoGeo? endereco) =
            EnderecoGeoInputMapping.Resolver(command.Endereco, existente: null, agora);

        // Não retorna cedo quando o endereço falha: LocalOferta.Criar precisa
        // rodar de qualquer forma (com endereco: null quando inválido) para que
        // tipo/cidade/codigoEmec também sejam avaliados e entrem no mesmo lote de
        // errors[] — senão um payload com CEP malformado E tipo inválido só
        // reportaria o erro de endereço.
        Result<LocalOferta> localResult = LocalOferta.Criar(
            command.Tipo,
            command.CampusResponsavelId,
            command.CidadeCodigoIbge,
            command.CidadeNome,
            command.CidadeUf,
            ReferenciaCidadeGeo.OrigemGeoApi,
            agora,
            enderecoErro is null ? endereco : null,
            command.CodigoEmec);

        if (enderecoErro is not null || localResult.IsFailure)
        {
            List<FieldError> erros = [.. localResult.Errors];
            if (enderecoErro is not null)
            {
                erros.Add(new FieldError("endereco", enderecoErro));
            }

            return Result<Guid>.ValidationFailure(erros);
        }

        LocalOferta local = localResult.Value!;

        if (command.CampusResponsavelId.HasValue
            && !await campusRepository.ExisteVivoAsync(command.CampusResponsavelId.Value, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(new DomainError(
                LocalOfertaErrorCodes.CampusResponsavelNaoEncontrado,
                "O Campus responsável informado não foi encontrado."));
        }

        await repository.AdicionarAsync(local, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(local.Id);
    }
}
