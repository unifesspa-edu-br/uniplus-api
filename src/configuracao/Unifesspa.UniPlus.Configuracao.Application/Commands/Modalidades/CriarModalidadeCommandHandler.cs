namespace Unifesspa.UniPlus.Configuracao.Application.Commands.Modalidades;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarModalidadeCommand"/> (convention-based Wolverine).
/// Orquestra: unicidade do código entre vivos (409), construção do agregado
/// (invariantes de coerência, 422), integridade referencial dos códigos citados em
/// composição/remanejamento (422), persistência e commit. Protege a corrida
/// check-then-act traduzindo a violação do índice único parcial em
/// <c>CodigoJaExiste</c>.
/// </summary>
public static class CriarModalidadeCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarModalidadeCommand command,
        IModalidadeRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        if (await repository.CodigoExisteEntreVivosAsync(command.Codigo, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExisteErro(command.Codigo));
        }

        Result<Modalidade> modalidadeResult = Modalidade.Criar(
            command.Codigo,
            command.Descricao,
            command.NaturezaLegal,
            command.ComposicaoVagas,
            command.ComposicaoOrigem,
            command.RegraRemanejamento,
            command.RemanejamentoDestino,
            command.RemanejamentoPar,
            command.RemanejamentoFallback,
            command.CriteriosCumulativos,
            command.AcaoQuandoIndeferido,
            command.BaseLegal);

        if (modalidadeResult.IsFailure)
        {
            return Result<Guid>.Failure(modalidadeResult.Error!);
        }

        Modalidade modalidade = modalidadeResult.Value!;

        // Integridade referencial (invariante 7): todos os códigos citados (origem +
        // remanejamento) devem existir como modalidade viva.
        IReadOnlyCollection<string> referencias = ReferenciasDeModalidade.Coletar(modalidade);
        if (referencias.Count > 0
            && !await repository.CodigosVivosExistemAsync(referencias, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(ReferenciaInexistenteErro());
        }

        await repository.AdicionarAsync(modalidade, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsCodigoConflict(constraint))
        {
            // Corrida entre CodigoExisteEntreVivosAsync e o INSERT (check-then-act): o
            // índice único parcial dispara 23505 e viramos o mesmo CodigoJaExiste do
            // caminho não-race — 409 consistente. O filtro do `when` garante que
            // outras exceções propagam intactas.
            return Result<Guid>.Failure(CodigoJaExisteErro(command.Codigo));
        }

        return Result<Guid>.Success(modalidade.Id);
    }

    private static DomainError CodigoJaExisteErro(string codigo) =>
        new(ModalidadeErrorCodes.CodigoJaExiste,
            $"Já existe uma modalidade viva com o código '{codigo}'.");

    private static DomainError ReferenciaInexistenteErro() =>
        new(ModalidadeErrorCodes.ReferenciaInexistenteOuInativa,
            "Um ou mais códigos de modalidade referenciados (origem ou remanejamento) "
            + "não correspondem a modalidades vivas.");
}
