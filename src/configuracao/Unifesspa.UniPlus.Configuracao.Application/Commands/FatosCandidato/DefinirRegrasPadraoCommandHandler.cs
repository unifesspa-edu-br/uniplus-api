namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Application.Commands.Predicados;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Monta as regras a partir da entrada, com as recusas de forma acumuladas por regra (ADR-0125), e
/// só então as confere contra o catálogo inteiro e as precedências entre fases.
/// </summary>
public static class DefinirRegrasPadraoCommandHandler
{
    public static async Task<Result> Handle(
        DefinirRegrasPadraoCommand command,
        IFatoCandidatoRepository repository,
        IPrecedenciaFaseRepository precedenciaRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(precedenciaRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<IReadOnlyList<RegraDerivacao>> regras = Montar(command.Regras);

        return regras.IsFailure
            ? Result.ValidationFailure(regras.Errors)
            : await GravarConferidasAsync(
                command.Id, regras.Value!, repository, precedenciaRepository, unitOfWork, cancellationToken)
                .ConfigureAwait(false);
    }

    private static async Task<Result> GravarConferidasAsync(
        Guid id,
        IReadOnlyList<RegraDerivacao> regras,
        IFatoCandidatoRepository repository,
        IPrecedenciaFaseRepository precedenciaRepository,
        IUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        // Duas regravações simultâneas, cada uma sem ciclo contra o catálogo que leu, podiam fechar
        // um ciclo juntas: as escritas de regras padrão são serializadas.
        await repository.TravarRegrasPadraoParaEscritaAsync(cancellationToken).ConfigureAwait(false);
        CatalogoDeFatos catalogo = await CarregarCatalogoAsync(repository, precedenciaRepository, cancellationToken).ConfigureAwait(false);

        return await MutacaoDoFato.AplicarAsync(
            id, fato => fato.DefinirRegrasPadrao(regras, catalogo), repository, unitOfWork, cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<CatalogoDeFatos> CarregarCatalogoAsync(
        IFatoCandidatoRepository repository, IPrecedenciaFaseRepository precedenciaRepository, CancellationToken cancellationToken)
    {
        IReadOnlyList<FatoCandidato> fatos = await repository.ListarTodosAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<PrecedenciaFase> precedencias = await precedenciaRepository.ListarVivasAsync(cancellationToken).ConfigureAwait(false);

        return new CatalogoDeFatos(fatos, precedencias);
    }

    private static Result<IReadOnlyList<RegraDerivacao>> Montar(IReadOnlyList<RegraPadraoInput>? entradas) =>
        entradas is null
            ? Result<IReadOnlyList<RegraDerivacao>>.Failure(new DomainError(
                FatoCandidatoErrorCodes.RegraPadraoMalformada, "A lista de regras é obrigatória; a lista vazia remove as regras."))
            : MontarTodas(entradas);

    private static Result<IReadOnlyList<RegraDerivacao>> MontarTodas(IReadOnlyList<RegraPadraoInput> entradas)
    {
        List<Result<RegraDerivacao>> montadas = [.. entradas.Select(MontarRegra)];
        List<FieldError> erros = [.. montadas.SelectMany(ErrosDaRegra)];

        return erros.Count > 0
            ? Result<IReadOnlyList<RegraDerivacao>>.ValidationFailure(erros)
            : Result<IReadOnlyList<RegraDerivacao>>.Success([.. montadas.Select(regra => regra.Value!)]);
    }

    private static IEnumerable<FieldError> ErrosDaRegra(Result<RegraDerivacao> regra, int posicao) =>
        regra.IsSuccess
            ? []
            : regra.Errors.Select(e => e.NoCaminhoDoItem($"regras[{posicao}]"));

    private static Result<RegraDerivacao> MontarRegra(RegraPadraoInput? entrada)
    {
        if (entrada is null)
        {
            return Result<RegraDerivacao>.Failure(RegraNula());
        }

        Result<PredicadoDnf> predicado = MontagemDoPredicado.Montar(entrada.Quando, RegraNula());
        if (predicado.IsFailure)
        {
            return Result<RegraDerivacao>.ValidationFailure([.. predicado.Errors.Select(e => e.NoCaminhoDoItem("quando"))]);
        }

        return entrada.Contribui is null
            ? Result<RegraDerivacao>.Success(RegraDerivacao.CriarBooleana(predicado.Value!))
            : RegraDerivacao.Criar(predicado.Value!, entrada.Contribui);
    }

    private static DomainError RegraNula() => new(
        FatoCandidatoErrorCodes.RegraPadraoMalformada, "A regra ou uma das suas condições veio nula.");
}
