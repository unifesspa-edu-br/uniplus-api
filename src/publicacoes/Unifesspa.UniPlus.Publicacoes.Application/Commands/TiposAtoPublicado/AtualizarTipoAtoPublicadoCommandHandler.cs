namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Abstractions;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

/// <summary>
/// Handler do <see cref="AtualizarTipoAtoPublicadoCommand"/>. Valida os campos
/// independentes do payload ANTES de qualquer I/O — validação sempre vence 404 —
/// e só então busca o tipo por Id (a checagem de imutabilidade do código depende
/// do valor persistido, então roda depois do fetch). Revalida a sobreposição
/// apenas quando o código ou a janela mudam — alterar o nome ou os atributos de
/// consequência não pode criar conflito.
/// </summary>
public static class AtualizarTipoAtoPublicadoCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarTipoAtoPublicadoCommand command,
        ITipoAtoPublicadoRepository repository,
        IPublicacoesUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result validacaoPreCheck = TipoAtoPublicado.ValidarCampos(
            command.Codigo, command.Nome, command.VigenciaInicio, command.VigenciaFim, command.BaseLegal);
        if (validacaoPreCheck.IsFailure)
        {
            return Result.ValidationFailure(validacaoPreCheck.Errors);
        }

        TipoAtoPublicado? tipo = await repository
            .ObterPorIdAsync(command.Id, cancellationToken)
            .ConfigureAwait(false);
        if (tipo is null)
        {
            return Result.Failure(NaoEncontradoErro());
        }

        if (!string.Equals(tipo.Codigo, command.Codigo!.Trim(), StringComparison.Ordinal))
        {
            return Result.Failure(new DomainError(
                TipoAtoPublicadoErrorCodes.CodigoImutavel,
                "O código do tipo de ato é a sua identidade e não muda: a série de vigências "
                + "agrupa-se por ele, e é por ele que um objeto reserva a vaga de um ato único. "
                + "Para um tipo diferente, cadastre um tipo novo."));
        }

        // O código já foi confirmado inalterado acima — só a janela pode ter mudado.
        if (JanelaMudou(tipo, command))
        {
            bool sobreposta = await repository.ExisteSobreposicaoDeVigenciaAsync(
                command.Codigo, command.VigenciaInicio, command.VigenciaFim, command.Id, cancellationToken)
                .ConfigureAwait(false);
            if (sobreposta)
            {
                return Result.Failure(CriarTipoAtoPublicadoCommandHandler.VigenciaSobrepostaErro(command.Codigo));
            }
        }

        Result atualizacao = tipo.Atualizar(
            command.Codigo,
            command.Nome,
            command.CongelaConfiguracao,
            command.UnicoPorObjeto,
            command.EfeitoIrreversivel,
            command.VigenciaInicio,
            command.VigenciaFim,
            command.BaseLegal);

        if (atualizacao.IsFailure)
        {
            return atualizacao;
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ExclusionConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && ExclusionConstraintViolation.IsVigenciaConflict(constraint))
        {
            // A exclusion constraint é DEFERRABLE INITIALLY IMMEDIATE — checada
            // dentro do próprio SalvarAlteracoesAsync acima, não num commit
            // externo (diferente de constraints INITIALLY DEFERRED). Descarta o
            // rastreamento do agregado Modified ANTES de devolver a falha: o
            // SaveChangesAsync automático do outbox do Wolverine
            // (AutoApplyTransactions, ADR-0004) roda de novo depois que este
            // handler retorna, e sem isso a mesma exceção estouraria de novo
            // fora deste catch, virando 500 em vez do DomainError já traduzido.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(CriarTipoAtoPublicadoCommandHandler.VigenciaSobrepostaErro(command.Codigo));
        }

        return Result.Success();
    }

    private static bool JanelaMudou(TipoAtoPublicado tipo, AtualizarTipoAtoPublicadoCommand command) =>
        tipo.VigenciaInicio != command.VigenciaInicio || tipo.VigenciaFim != command.VigenciaFim;

    private static DomainError NaoEncontradoErro() =>
        new(TipoAtoPublicadoErrorCodes.NaoEncontrado, "Tipo de ato não encontrado.");
}
