namespace Unifesspa.UniPlus.Configuracao.Application.Commands.CategoriasDocumento;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarCategoriaDocumentoCommand"/>. Valida os campos
/// editáveis primeiro (sem I/O) — validação sempre vence 404 — só então busca o
/// registro por Id; como o código é editável, confere a unicidade entre categorias
/// vivas quando ele muda (ignorando o próprio registro) e protege a corrida
/// traduzindo a violação do índice único parcial em <c>CodigoJaExiste</c>.
/// </summary>
public static class AtualizarCategoriaDocumentoCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarCategoriaDocumentoCommand command,
        ICategoriaDocumentoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<(CodigoCategoriaDocumento Codigo, string Nome, string? Descricao, int Ordem)> campos =
            CategoriaDocumento.ValidarCamposEditaveis(command.Codigo, command.Nome, command.Descricao, command.Ordem);
        if (campos.IsFailure)
        {
            return Result.ValidationFailure(campos.Errors);
        }

        CategoriaDocumento? categoria = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (categoria is null)
        {
            return Result.Failure(new DomainError(
                CategoriaDocumentoErrorCodes.NaoEncontrada,
                "Categoria de documento não encontrada."));
        }

        // Código é case-sensitive (Ordinal) — só checa colisão quando o código
        // normalizado efetivamente muda em relação ao atual.
        if (!string.Equals(campos.Value.Codigo.Valor, categoria.Codigo.Valor, StringComparison.Ordinal)
            && await repository.CodigoExisteEntreVivosAsync(campos.Value.Codigo.Valor, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(CodigoJaExisteErro());
        }

        Result atualizarResult = categoria.Atualizar(command.Codigo, command.Nome, command.Descricao, command.Ordem);
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
        new(CategoriaDocumentoErrorCodes.CodigoJaExiste,
            "Já existe uma categoria de documento viva com o código informado.");
}
