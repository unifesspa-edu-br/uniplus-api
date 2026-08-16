namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CondicoesAtendimento;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarCondicaoAtendimentoCommand"/>. Valida código,
/// nome e descrição primeiro (sem I/O) — validação sempre vence 404 — só então
/// busca o registro por Id; avalia a transição de código contra o reservado
/// <c>PCD</c> (<c>CodigoProtegidoNaoEditavel</c>) antes mesmo de consultar
/// unicidade, já que essa consulta não faz sentido se a transição já é proibida;
/// como o código é editável, confere a unicidade entre condições vivas quando ele
/// muda (ignorando o próprio registro) e protege a corrida traduzindo a violação
/// do índice único parcial em <c>CodigoJaExiste</c>.
/// </summary>
public static class AtualizarCondicaoAtendimentoCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarCondicaoAtendimentoCommand command,
        ICondicaoAtendimentoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<(CodigoCondicao Codigo, string Nome, string? Descricao)> campos =
            CondicaoAtendimentoEspecializado.ValidarCamposEditaveis(command.Codigo, command.Nome, command.Descricao);
        if (campos.IsFailure)
        {
            return Result.ValidationFailure(campos.Errors);
        }

        CondicaoAtendimentoEspecializado? condicao = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (condicao is null)
        {
            return Result.Failure(new DomainError(
                CondicaoAtendimentoErrorCodes.NaoEncontrada,
                "Condição de atendimento especializado não encontrada."));
        }

        Result transicao = condicao.ValidarTransicaoDeCodigo(campos.Value.Codigo);
        if (transicao.IsFailure)
        {
            return transicao;
        }

        // Código é case-sensitive (Ordinal) — só checa colisão quando o código
        // normalizado efetivamente muda em relação ao atual.
        if (!string.Equals(campos.Value.Codigo.Valor, condicao.Codigo.Valor, StringComparison.Ordinal)
            && await repository.CodigoExisteEntreVivosAsync(campos.Value.Codigo.Valor, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(CodigoJaExisteErro());
        }

        Result atualizarResult = condicao.Atualizar(command.Codigo, command.Nome, command.Descricao);
        if (atualizarResult.IsFailure)
        {
            return atualizarResult;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsCodigoConflict(constraint))
        {
            // Corrida entre a checagem de unicidade e o UPDATE: o índice único parcial
            // dispara 23505; sem descartar, o SaveChangesAsync automático do Wolverine
            // repetiria o mesmo UPDATE fora deste catch e o 409 pretendido viraria 500.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(CodigoJaExisteErro());
        }

        return Result.Success();
    }

    private static DomainError CodigoJaExisteErro() =>
        new(CondicaoAtendimentoErrorCodes.CodigoJaExiste,
            "Já existe uma condição de atendimento especializado viva com o código informado.");
}
