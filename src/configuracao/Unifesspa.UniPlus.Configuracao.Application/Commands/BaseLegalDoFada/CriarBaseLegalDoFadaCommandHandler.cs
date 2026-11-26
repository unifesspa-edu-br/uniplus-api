namespace Unifesspa.UniPlus.Configuracao.Application.Commands.BaseLegalDoFada;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Persistence;
using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

public static class CriarBaseLegalDoFadaCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarBaseLegalDoFadaCommand command,
        IBaseLegalDoFadaRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<BaseLegalDoFada> criada = Criar(command);

        return criada.IsFailure
            ? Result<Guid>.ValidationFailure(criada.Errors)
            : await GravarAsync(criada.Value!, repository, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static Result<BaseLegalDoFada> Criar(CriarBaseLegalDoFadaCommand command)
    {
        // m pode ser null (JSON "municipios": [null]) — os options padrão não rejeitam elemento nulo em coleção genérica; sem
        // o ?., o projetado estouraria NullReferenceException em vez de virar 422 (ReferenciaCidadeGeo.Validar já recusa
        // código/nome/UF nulos como campo obrigatório ausente).
        IEnumerable<(string? CodigoIbge, string? Nome, string? Uf)>? municipios =
            command.Municipios?.Select(m => (m?.CodigoIbge, m?.Nome, m?.Uf));

        return BaseLegalDoFada.Criar(command.TipoInstrumento, command.Identificacao, command.Descricao, municipios);
    }

    // O código é único entre as bases legais vivas: a conferência prévia dá a recusa comum e o índice único decide a corrida.
    private static async Task<Result<Guid>> GravarAsync(
        BaseLegalDoFada entity,
        IBaseLegalDoFadaRepository repository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        if (await repository.CodigoExisteAsync(entity.Codigo, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExiste(entity));
        }

        await repository.AdicionarAsync(entity, cancellationToken).ConfigureAwait(false);

        return await SalvarAsync(entity, unitOfWork, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Result<Guid>> SalvarAsync(BaseLegalDoFada entity, IUnitOfWork unitOfWork, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result<Guid>.Success(entity.Id);
        }
        catch (Exception exception) when (ConstraintVioladaNaGravacao.NomeDaConstraintViolada(exception)
            == RestricoesDoBanco.BaseLegalDoFadaCodigoAtivos)
        {
            // Sem descartar, a inserção continua rastreada e o SaveChanges do outbox a repete fora deste catch (500 em vez de 409).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(CodigoJaExiste(entity));
        }
    }

    private static DomainError CodigoJaExiste(BaseLegalDoFada entity) => new(
        BaseLegalDoFadaErrorCodes.CodigoJaExiste,
        $"Já existe uma base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) com o código {entity.Codigo.Valor}: o mesmo ato já está cadastrado.");
}
