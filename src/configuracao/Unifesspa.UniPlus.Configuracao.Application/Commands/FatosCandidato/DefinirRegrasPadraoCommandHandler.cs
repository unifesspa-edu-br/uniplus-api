namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
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
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(precedenciaRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        if (command.Regras is null)
        {
            return Result.Failure(new DomainError(
                FatoCandidatoErrorCodes.RegraPadraoMalformada, "A lista de regras é obrigatória; a lista vazia remove as regras."));
        }

        Result<IReadOnlyList<RegraDerivacao>> regras = Montar(command.Regras);
        if (regras.IsFailure)
        {
            return Result.ValidationFailure(regras.Errors);
        }

        // Duas regravações simultâneas, cada uma sem ciclo contra o catálogo que leu, podiam fechar
        // um ciclo juntas: as escritas de regras padrão são serializadas.
        await repository.TravarRegrasPadraoParaEscritaAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<FatoCandidato> fatos = await repository.ListarTodosAsync(cancellationToken).ConfigureAwait(false);
        IReadOnlyList<PrecedenciaFase> precedencias = await precedenciaRepository.ListarVivasAsync(cancellationToken).ConfigureAwait(false);
        CatalogoDeFatos catalogo = new(fatos, precedencias);

        return await MutacaoDoFato.AplicarAsync(
            command.Id, fato => fato.DefinirRegrasPadrao(regras.Value!, catalogo), repository, unitOfWork, cancellationToken)
            .ConfigureAwait(false);
    }

    private static Result<IReadOnlyList<RegraDerivacao>> Montar(IReadOnlyList<RegraPadraoInput> entradas)
    {
        List<FieldError> erros = [];
        List<RegraDerivacao> regras = [];
        for (int i = 0; i < entradas.Count; i++)
        {
            Result<RegraDerivacao> regra = MontarRegra(entradas[i]);
            if (regra.IsFailure)
            {
                erros.AddRange(regra.Errors.Select(e => new FieldError($"regras[{i}]", e.Error)));
            }
            else
            {
                regras.Add(regra.Value!);
            }
        }

        return erros.Count > 0
            ? Result<IReadOnlyList<RegraDerivacao>>.ValidationFailure(erros)
            : Result<IReadOnlyList<RegraDerivacao>>.Success(regras);
    }

    private static Result<RegraDerivacao> MontarRegra(RegraPadraoInput? entrada)
    {
        if (entrada is null)
        {
            return Result<RegraDerivacao>.Failure(RegraNula());
        }

        List<(int Clausula, CondicaoDnf Condicao)> linhas = [];
        IReadOnlyList<IReadOnlyList<CondicaoRegraPadraoInput>> quando = entrada.Quando ?? [];
        for (int clausula = 0; clausula < quando.Count; clausula++)
        {
            // Cláusula vazia é recusada pelo predicado; a regra incondicional é o quando vazio.
            if (quando[clausula] is not { Count: > 0 } condicoes)
            {
                return Result<RegraDerivacao>.Failure(new DomainError(
                    "ClausulaDnf.ClausulaVazia", "Uma cláusula deve ter ao menos uma condição."));
            }

            foreach (CondicaoRegraPadraoInput? condicao in condicoes)
            {
                if (condicao is null)
                {
                    return Result<RegraDerivacao>.Failure(RegraNula());
                }

                Result<CondicaoDnf> criada = CondicaoDnf.Criar(condicao.Fato, OperadorCodigo.FromCodigo(condicao.Operador), condicao.Valor);
                if (criada.IsFailure)
                {
                    return Result<RegraDerivacao>.Failure(criada.Error!);
                }

                linhas.Add((clausula, criada.Value!));
            }
        }

        Result<PredicadoDnf> predicado = PredicadoDnf.CriarDeCondicoesAgrupadas(linhas);
        if (predicado.IsFailure)
        {
            return Result<RegraDerivacao>.Failure(predicado.Error!);
        }

        return entrada.Contribui is null
            ? Result<RegraDerivacao>.Success(RegraDerivacao.CriarBooleana(predicado.Value!))
            : RegraDerivacao.Criar(predicado.Value!, entrada.Contribui);
    }

    private static DomainError RegraNula() => new(
        FatoCandidatoErrorCodes.RegraPadraoMalformada, "A regra ou uma das suas condições veio nula.");
}
