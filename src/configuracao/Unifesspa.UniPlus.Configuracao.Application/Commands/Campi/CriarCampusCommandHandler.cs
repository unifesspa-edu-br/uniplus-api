namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Campi;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Enderecos;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarCampusCommand"/> (convention-based Wolverine):
/// valida o agregado (sem I/O), só então confere a unicidade da sigla entre
/// campi vivos, cria carimbando a proveniência do display cache
/// (<c>cidade_origem = "geo-api"</c>) + instante a partir do
/// <see cref="TimeProvider"/>, persiste e commita.
/// </summary>
/// <remarks>
/// A checagem de unicidade roda depois da validação de campo (não antes, como
/// no desenho anterior) — evita consultar o repositório com um comando que já
/// se sabe inválido por outro motivo (ADR-0125).
/// </remarks>
public static class CriarCampusCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarCampusCommand command,
        ICampusRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        DateTimeOffset agora = timeProvider.GetUtcNow();

        (DomainError? enderecoErro, ReferenciaEnderecoGeo? endereco) =
            EnderecoGeoInputMapping.Resolver(command.Endereco, existente: null, agora);

        // Não retorna cedo quando o endereço falha: Campus.Criar precisa rodar de
        // qualquer forma (com endereco: null quando inválido — ValidarCoerencia trata
        // "sem endereço" como coerente por definição) para que sigla/nome/cidade
        // também sejam avaliados e entrem no mesmo lote de errors[]. Sem isso, um
        // payload com CEP malformado E sigla vazia só reportaria o erro de endereço.
        Result<Campus> campusResult = Campus.Criar(
            command.Sigla,
            command.Nome,
            command.CidadeCodigoIbge,
            command.CidadeNome,
            command.CidadeUf,
            ReferenciaCidadeGeo.OrigemGeoApi,
            agora,
            enderecoErro is null ? endereco : null,
            command.CodigoEmec);

        if (enderecoErro is not null || campusResult.IsFailure)
        {
            List<FieldError> erros = [.. campusResult.Errors];
            if (enderecoErro is not null)
            {
                erros.Add(new FieldError("endereco", enderecoErro));
            }

            return Result<Guid>.ValidationFailure(erros);
        }

        Campus campus = campusResult.Value!;

        if (await repository.SiglaExisteEntreLivosAsync(campus.Sigla, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(new DomainError(
                CampusErrorCodes.SiglaJaExiste,
                $"Já existe um Campus vivo com a sigla '{campus.Sigla}'."));
        }

        await repository.AdicionarAsync(campus, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        return Result<Guid>.Success(campus.Id);
    }
}
