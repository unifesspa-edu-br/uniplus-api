namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CategoriasDocumento;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarCategoriaDocumentoCommand"/> (convention-based
/// Wolverine): valida o agregado por inteiro primeiro (sem I/O) — código, nome,
/// descrição e ordem acumulam no mesmo lote — só então confere a unicidade do
/// código entre vivas, com o código já normalizado. Protege a corrida
/// check-then-act traduzindo a violação do índice único parcial em
/// <c>CodigoJaExiste</c>.
/// </summary>
public static class CriarCategoriaDocumentoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarCategoriaDocumentoCommand command,
        ICategoriaDocumentoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<CategoriaDocumento> criar = CategoriaDocumento.Criar(
            command.Codigo, command.Nome, command.Descricao, command.Ordem);
        if (criar.IsFailure)
        {
            return Result<Guid>.ValidationFailure(criar.Errors);
        }

        CategoriaDocumento categoria = criar.Value!;

        if (await repository.CodigoExisteEntreVivosAsync(categoria.Codigo.Valor, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExisteErro());
        }

        await repository.AdicionarAsync(categoria, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsCodigoConflict(constraint))
        {
            // Sem descartar, a entidade Added continua rastreada e o SaveChangesAsync
            // automático do Wolverine (AutoApplyTransactions) tenta a mesma inserção de
            // novo FORA deste catch — a mesma violação estoura sem tradução, e o 409
            // pretendido vira 500.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(CodigoJaExisteErro());
        }

        return Result<Guid>.Success(categoria.Id);
    }

    private static DomainError CodigoJaExisteErro() =>
        new(CategoriaDocumentoErrorCodes.CodigoJaExiste,
            "Já existe uma categoria de documento viva com o código informado.");
}
