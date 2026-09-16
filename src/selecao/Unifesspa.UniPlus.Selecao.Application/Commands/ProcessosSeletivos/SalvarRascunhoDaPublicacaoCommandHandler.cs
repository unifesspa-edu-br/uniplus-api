namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Handler de <see cref="SalvarRascunhoDaPublicacaoCommand"/>.
/// </summary>
/// <remarks>
/// Não toca o agregado: não carrega <c>ProcessoSeletivo</c> para mutação, não abre sessão
/// editorial, não incrementa revisão nenhuma. Guardar um bloco de notas não é editar a
/// configuração do certame, e acoplar as duas coisas faria o rascunho esbarrar na concorrência
/// editorial de uma retificação em curso — impedindo de salvar exatamente quem mais precisa.
/// </remarks>
public static class SalvarRascunhoDaPublicacaoCommandHandler
{
    public static async Task<Result> Handle(
        SalvarRascunhoDaPublicacaoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRascunhoDePublicacaoRepository rascunhoRepository,
        ISelecaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(rascunhoRepository);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (DonoDoRascunho(userContext) is not { } usuarioSub)
        {
            return Result.Failure(RascunhoDaPublicacao.SemDono);
        }

        // A existência do processo é conferida porque a alternativa é pior que um round-trip:
        // a violação de chave estrangeira estouraria como 500 na gravação, e o operador que
        // errou o endereço veria falha de servidor em vez de "não encontrado".
        if (!await processoSeletivoRepository.ExisteAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(RascunhoDaPublicacao.ProcessoNaoEncontrado(command.ProcessoSeletivoId));
        }

        string conteudo = command.Conteudo.GetRawText();
        DateTimeOffset agora = timeProvider.GetUtcNow();

        RascunhoDePublicacao? existente = await rascunhoRepository
            .ObterDoOperadorAsync(command.ProcessoSeletivoId, usuarioSub, cancellationToken)
            .ConfigureAwait(false);

        if (existente is null)
        {
            Result<RascunhoDePublicacao> criacao = RascunhoDePublicacao.Criar(
                command.ProcessoSeletivoId, usuarioSub, conteudo, command.Versao, agora, RascunhoDePublicacao.Prazo);
            if (criacao.IsFailure)
            {
                return Result.Failure(criacao.Error!);
            }

            await rascunhoRepository.AdicionarAsync(criacao.Value!, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // Um rascunho vencido que o dono regrava volta a valer, com prazo novo: ele está
            // ali, trabalhando nele. Vencimento é o que apaga o abandonado, não o que impede o
            // retorno de quem nunca saiu.
            Result substituicao = existente.Substituir(conteudo, command.Versao, agora, RascunhoDePublicacao.Prazo);
            if (substituicao.IsFailure)
            {
                return substituicao;
            }

            rascunhoRepository.Atualizar(existente);
        }

        // Cada gravação leva junto os rascunhos que já venceram — de qualquer processo, de
        // qualquer operador. É o que faz o prazo valer para o caso que mais importa: o certame
        // abandonado no meio, que ninguém volta a abrir e cuja expiração na leitura nunca
        // aconteceria. Sai antes do SaveChanges para ir na mesma transação.
        await rascunhoRepository.ApagarVencidosAsync(agora, cancellationToken).ConfigureAwait(false);

        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    internal static string? DonoDoRascunho(IUserContext userContext) =>
        string.IsNullOrWhiteSpace(userContext.UserId) ? null : userContext.UserId;
}
