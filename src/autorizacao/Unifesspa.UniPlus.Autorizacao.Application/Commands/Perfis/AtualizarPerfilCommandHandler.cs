namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Handler da edição de perfil: substitui nome, descrição e permissões.</summary>
public static class AtualizarPerfilCommandHandler
{
    public static async Task<Result> Handle(
        AtualizarPerfilCommand command,
        IPerfilRepository repository,
        ICatalogoDePermissoesConsultavel catalogo,
        IAutorizacaoUnitOfWork unitOfWork,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(unitOfWork);

        Perfil? perfil = await repository.ObterPorIdAsync(command.Id, cancellationToken).ConfigureAwait(false);
        if (perfil is null)
        {
            return Result.Failure(PerfilHandlerErros.NaoEncontrado(command.Id));
        }

        Result atualizar = perfil.Atualizar(command.Nome, command.Descricao, command.Permissoes, catalogo);
        if (atualizar.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return atualizar;
        }

        if (await repository.NomeExisteAsync(perfil.Nome, exceto: perfil.Id, cancellationToken).ConfigureAwait(false))
        {
            // O perfil já foi mutado em memória e segue rastreado: sem o descarte, a
            // gravação que o outbox dispara depois do handler persistiria o nome repetido.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(PerfilHandlerErros.NomeDuplicado());
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (PerfilViolacoes.EhConflitoDeNome(ex))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(PerfilHandlerErros.NomeDuplicado());
        }
        catch (Exception ex) when (PerfilViolacoes.EhConflitoDeConcorrencia(ex))
        {
            // Duas edições a partir da mesma versão não se misturam: a segunda recebe o
            // conflito em vez de sobrescrever a lista de permissões da primeira. Traduzir aqui,
            // e não deixar propagar, completa a entrada de idempotência normalmente.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(PerfilHandlerErros.ConflitoDeConcorrencia());
        }

        return Result.Success();
    }
}
