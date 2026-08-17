namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Abstractions;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

/// <summary>
/// Handler do <see cref="CriarTipoAtoPublicadoCommand"/> (convention-based
/// Wolverine): valida o payload por inteiro primeiro (sem I/O — validação sempre
/// vence I/O), só então confere que nenhuma versão viva do mesmo código intercepta
/// a janela informada, cria o agregado (que não pode falhar de novo, já validado),
/// persiste e commita.
/// </summary>
/// <remarks>
/// A consulta prévia dá a mensagem legível no caso comum; a exclusion constraint
/// fecha a corrida check-then-act. São papéis distintos, não duplicação: entre a
/// consulta e o <c>SaveChanges</c> cabe outra transação, e só o banco a vê.
/// </remarks>
public static class CriarTipoAtoPublicadoCommandHandler
{
    public static async Task<Result<Guid>> Handle(
        CriarTipoAtoPublicadoCommand command,
        ITipoAtoPublicadoRepository repository,
        IPublicacoesUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Result validacao = TipoAtoPublicado.ValidarCampos(
            command.Codigo, command.Nome, command.VigenciaInicio, command.VigenciaFim, command.BaseLegal);
        if (validacao.IsFailure)
        {
            return Result<Guid>.ValidationFailure(validacao.Errors);
        }

        bool sobreposta = await repository.ExisteSobreposicaoDeVigenciaAsync(
            command.Codigo!, command.VigenciaInicio, command.VigenciaFim, null, cancellationToken)
            .ConfigureAwait(false);
        if (sobreposta)
        {
            return Result<Guid>.Failure(VigenciaSobrepostaErro(command.Codigo!));
        }

        TipoAtoPublicado tipo = TipoAtoPublicado.Criar(
            command.Codigo,
            command.Nome,
            command.CongelaConfiguracao,
            command.UnicoPorObjeto,
            command.EfeitoIrreversivel,
            command.VigenciaInicio,
            command.VigenciaFim,
            command.BaseLegal).Value!;

        await repository.AdicionarAsync(tipo, cancellationToken).ConfigureAwait(false);

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
            // rastreamento do agregado Added ANTES de devolver a falha: o
            // SaveChangesAsync automático do outbox do Wolverine
            // (AutoApplyTransactions, ADR-0004) roda de novo depois que este
            // handler retorna, e sem isso a mesma exceção estouraria de novo
            // fora deste catch, virando 500 em vez do DomainError já traduzido.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<Guid>.Failure(VigenciaSobrepostaErro(command.Codigo!));
        }

        return Result<Guid>.Success(tipo.Id);
    }

    internal static DomainError VigenciaSobrepostaErro(string codigo) =>
        new(TipoAtoPublicadoErrorCodes.VigenciaSobreposta,
            $"Já existe uma versão viva do tipo de ato '{codigo}' vigente em parte do período informado.");
}
