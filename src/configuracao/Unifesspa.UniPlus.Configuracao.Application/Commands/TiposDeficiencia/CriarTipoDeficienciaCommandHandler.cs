namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="CriarTipoDeficienciaCommand"/> (convention-based
/// Wolverine): valida o agregado por inteiro primeiro (sem I/O) — código, nome e
/// descrição acumulam no mesmo lote — só então confere a unicidade do código e a
/// do nome entre vivos, com os valores já normalizados. Protege a corrida
/// check-then-act traduzindo a violação de cada índice único parcial no conflito
/// correspondente.
/// </summary>
public static class CriarTipoDeficienciaCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarTipoDeficienciaCommand command,
        ITipoDeficienciaRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<TipoDeficiencia> criar = TipoDeficiencia.Criar(
            command.Codigo, command.Nome, command.Descricao, command.Permanente);
        if (criar.IsFailure)
        {
            return Result<Guid>.ValidationFailure(criar.Errors);
        }

        TipoDeficiencia tipo = criar.Value!;

        if (await repository.CodigoExisteEntreVivosAsync(tipo.Codigo.Valor, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(CodigoJaExisteErro());
        }

        if (await repository.NomeExisteEntreVivosAsync(tipo.Nome, null, cancellationToken).ConfigureAwait(false))
        {
            return Result<Guid>.Failure(NomeJaExisteErro());
        }

        await repository.AdicionarAsync(tipo, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ConflitoDeUnicidade(ex) is { } conflito)
        {
            // Sem descartar, a entidade Added continua rastreada e o SaveChangesAsync
            // automático do Wolverine (AutoApplyTransactions) tenta a mesma inserção de
            // novo FORA deste catch — a mesma violação estoura sem tradução, e o 409
            // pretendido vira 500.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(conflito);
        }

        return Result<Guid>.Success(tipo.Id);
    }

    /// <summary>
    /// Traduz a violação 23505 no conflito da constraint efetivamente violada —
    /// há dois índices únicos parciais na tabela, e devolver o erro do outro
    /// mentiria sobre a causa. <see langword="null"/> quando a exceção não é uma
    /// violação de unicidade conhecida (o caller deixa propagar).
    /// </summary>
    private static DomainError? ConflitoDeUnicidade(Exception ex)
    {
        if (UniqueConstraintViolation.GetViolatedConstraint(ex) is not { } constraint)
        {
            return null;
        }

        if (UniqueConstraintViolation.IsCodigoConflict(constraint))
        {
            return CodigoJaExisteErro();
        }

        return UniqueConstraintViolation.IsNomeConflict(constraint) ? NomeJaExisteErro() : null;
    }

    private static DomainError CodigoJaExisteErro() =>
        new(TipoDeficienciaErrorCodes.CodigoJaExiste,
            "Já existe um tipo de deficiência vivo com o código informado.");

    private static DomainError NomeJaExisteErro() =>
        new(TipoDeficienciaErrorCodes.NomeJaExiste,
            "Já existe um tipo de deficiência vivo com o nome informado.");
}
