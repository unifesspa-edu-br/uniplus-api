namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDeficiencia;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarTipoDeficienciaCommand"/>. Valida código, nome e
/// descrição primeiro (sem I/O) — validação sempre vence 404 — só então busca o
/// registro por Id; como código e nome são editáveis, confere a unicidade de cada
/// um entre tipos vivos quando ele muda (ignorando o próprio registro) e protege a
/// corrida traduzindo a violação de cada índice único parcial no conflito
/// correspondente.
/// </summary>
public static class AtualizarTipoDeficienciaCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarTipoDeficienciaCommand command,
        ITipoDeficienciaRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<(CodigoTipoDeficiencia Codigo, string Nome, string Descricao)> campos =
            TipoDeficiencia.ValidarCamposEditaveis(command.Codigo, command.Nome, command.Descricao);
        if (campos.IsFailure)
        {
            return Result.ValidationFailure(campos.Errors);
        }

        TipoDeficiencia? tipo = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return Result.Failure(new DomainError(
                TipoDeficienciaErrorCodes.NaoEncontrado,
                "Tipo de deficiência não encontrado."));
        }

        // Código é case-sensitive (Ordinal) — só checa colisão quando o código
        // normalizado efetivamente muda em relação ao atual.
        if (!string.Equals(campos.Value.Codigo.Valor, tipo.Codigo.Valor, StringComparison.Ordinal)
            && await repository.CodigoExisteEntreVivosAsync(campos.Value.Codigo.Valor, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(CodigoJaExisteErro());
        }

        // Nome é case-sensitive (Ordinal) — só checa colisão quando o nome
        // normalizado efetivamente muda em relação ao atual.
        if (!string.Equals(campos.Value.Nome, tipo.Nome, StringComparison.Ordinal)
            && await repository.NomeExisteEntreVivosAsync(campos.Value.Nome, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(NomeJaExisteErro());
        }

        Result atualizarResult = tipo.Atualizar(
            command.Codigo, command.Nome, command.Descricao, command.Permanente);
        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ConflitoDeUnicidade(ex) is { } conflito)
        {
            // Corrida entre a checagem de unicidade e o UPDATE: o índice único parcial
            // dispara 23505; sem descartar, o SaveChangesAsync automático do Wolverine
            // repetiria o mesmo UPDATE fora deste catch e o 409 pretendido viraria 500.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(conflito);
        }

        return Result.Success();
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
