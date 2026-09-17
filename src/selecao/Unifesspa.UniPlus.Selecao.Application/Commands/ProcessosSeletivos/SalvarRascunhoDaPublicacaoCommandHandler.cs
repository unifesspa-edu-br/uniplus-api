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
    /// <summary>O índice que admite um rascunho por operador em cada processo.</summary>
    private const string IndiceDoOperador = "ux_rascunhos_publicacao_processo_operador";

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
        //
        // Poupa o rascunho desta gravação: quando o dono volta a um que venceu, a renovação do
        // prazo ainda está só na entidade rastreada, e a varredura — SQL imediato — enxergaria a
        // data velha e apagaria a linha que o SaveChanges tentaria atualizar em seguida.
        await rascunhoRepository
            .ApagarVencidosAsync(agora, existente?.Id, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (string.Equals(
            UniqueConstraintViolation.GetViolatedConstraint(ex), IndiceDoOperador, StringComparison.Ordinal))
        {
            // Duas gravações do mesmo operador chegaram juntas: ambas leram que não havia
            // rascunho e ambas inseriram. O índice deixa passar uma — e a perda desta corrida
            // diz exatamente o que fazer: a linha existe agora, então basta reler e substituir,
            // que é o caminho que a leitura teria tomado se tivesse chegado um instante depois.
            //
            // Recusar aqui seria pior que inútil. O endpoint exige chave de idempotência, e o
            // filtro guarda a resposta de qualquer status abaixo de 500: a recusa ficaria
            // cacheada pelo prazo inteiro, e a retentativa sob a mesma chave receberia o
            // conflito de volta mesmo depois de a gravação ter passado a ser possível.
            //
            // Descarta o rastreamento antes de tentar de novo: sem isso o flush repetiria as
            // mesmas entradas em conflito (ADR-0119).
            unitOfWork.DescartarAlteracoesNaoSalvas();

            RascunhoDePublicacao? vencedor = await rascunhoRepository
                .ObterDoOperadorAsync(command.ProcessoSeletivoId, usuarioSub, cancellationToken)
                .ConfigureAwait(false);
            if (vencedor is null)
            {
                // A linha sumiu entre a colisão e a releitura — descarte ou expiração. Não há
                // segunda tentativa a fazer, e insistir arriscaria um laço.
                return Result.Failure(RascunhoDaPublicacao.GravacaoConcorrente);
            }

            Result substituicaoAposCorrida = vencedor.Substituir(
                conteudo, command.Versao, agora, RascunhoDePublicacao.Prazo);
            if (substituicaoAposCorrida.IsFailure)
            {
                return substituicaoAposCorrida;
            }

            rascunhoRepository.Atualizar(vencedor);
            await rascunhoRepository
                .ApagarVencidosAsync(agora, vencedor.Id, cancellationToken)
                .ConfigureAwait(false);
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    internal static string? DonoDoRascunho(IUserContext userContext) =>
        string.IsNullOrWhiteSpace(userContext.UserId) ? null : userContext.UserId;
}
