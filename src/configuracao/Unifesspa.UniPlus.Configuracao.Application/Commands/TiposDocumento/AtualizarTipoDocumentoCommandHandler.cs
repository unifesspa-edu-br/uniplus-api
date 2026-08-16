namespace Unifesspa.UniPlus.Configuracao.Application.Commands.TiposDocumento;

using Unifesspa.UniPlus.Configuracao.Application.Abstractions;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="AtualizarTipoDocumentoCommand"/>. Valida os campos (sem
/// I/O) antes de buscar a entidade: sem o validator removido, um payload mal
/// formado não pode chegar a <c>ObterPorIdAsync</c> nem à consulta de unicidade
/// primeiro — validação sempre vence sobre "não encontrado", mesma prioridade que
/// o validator garantia. Como o código é editável, confere a unicidade entre tipos
/// vivos quando ele muda (ignorando o próprio registro) — só depois de o agregado
/// passar em todas as regras de campo, para não mascarar violação de campo atrás
/// de um CodigoJaExiste — e protege a corrida traduzindo a violação do índice
/// único parcial em <c>CodigoJaExiste</c> (CA-02). O código antigo é capturado
/// antes de qualquer mutação, porque <see cref="TipoDocumento.Atualizar"/> muta o
/// agregado rastreado pelo EF.
/// </summary>
public static class AtualizarTipoDocumentoCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarTipoDocumentoCommand command,
        ITipoDocumentoRepository repository,
        IConfiguracaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result<(string Codigo, string Nome, string? Descricao, CategoriaDocumento Categoria, string? FormatosAceitos, int? TamanhoMaximoMb, string? TipoEquivalente)> validacao =
            TipoDocumento.ValidarCampos(
                command.Codigo,
                command.Nome,
                command.Descricao,
                command.Categoria,
                command.FormatosAceitos,
                command.TamanhoMaximoMb,
                command.TipoEquivalente);
        if (validacao.IsFailure)
        {
            return Result.ValidationFailure(validacao.Errors);
        }

        TipoDocumento? tipo = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            return Result.Failure(new DomainError(
                TipoDocumentoErrorCodes.NaoEncontrado,
                "Tipo de documento não encontrado."));
        }

        string codigoAntigo = tipo.Codigo;
        string codigoNovo = validacao.Value.Codigo;

        // Código é case-sensitive (Ordinal) — só checa colisão quando o código
        // normalizado efetivamente muda.
        if (!string.Equals(codigoAntigo, codigoNovo, StringComparison.Ordinal)
            && await repository.CodigoExisteEntreVivosAsync(codigoNovo, command.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(CodigoJaExisteErro());
        }

        // Revalida por dentro (barato, sem I/O) com exatamente os mesmos argumentos
        // já confirmados acima, então sempre terá sucesso aqui; esta chamada só
        // serve para aplicar a mutação.
        tipo.Atualizar(
            command.Codigo,
            command.Nome,
            command.Descricao,
            command.Categoria,
            command.FormatosAceitos,
            command.TamanhoMaximoMb,
            command.TipoEquivalente);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && UniqueConstraintViolation.IsCodigoConflict(constraint))
        {
            // Corrida entre a checagem de unicidade e o UPDATE: o índice único parcial
            // dispara 23505 e viramos o mesmo CodigoJaExiste do caminho não-race.
            return Result.Failure(CodigoJaExisteErro());
        }

        return Result.Success();
    }

    private static DomainError CodigoJaExisteErro() =>
        new(TipoDocumentoErrorCodes.CodigoJaExiste,
            "Já existe um tipo de documento vivo com o código informado.");
}
