namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json;

using Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Handler da <see cref="ObterRascunhoDaPublicacaoQuery"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>É aqui que o prazo é cobrado.</b> Não há job varrendo a tabela — o repositório não tem
/// infraestrutura de tarefa recorrente —, então quem lê é quem descarta o vencido. A
/// consequência aceita, e que precisa estar dita em algum lugar: o rascunho de um processo que
/// ninguém mais abre não é lido, logo não vence; ele desaparece quando o processo for publicado
/// ou o rascunho descartado.
/// </para>
/// <para>
/// O conteúdo volta como o cliente o escreveu, sem reserialização: ele é opaco também na volta.
/// </para>
/// </remarks>
public static class ObterRascunhoDaPublicacaoQueryHandler
{
    public static async Task<RascunhoDaPublicacaoDto?> Handle(
        ObterRascunhoDaPublicacaoQuery query,
        IRascunhoDePublicacaoRepository rascunhoRepository,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(rascunhoRepository);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (SalvarRascunhoDaPublicacaoCommandHandler.DonoDoRascunho(userContext) is not { } usuarioSub)
        {
            return null;
        }

        RascunhoDePublicacao? rascunho = await rascunhoRepository
            .ObterDoOperadorAsync(query.ProcessoSeletivoId, usuarioSub, cancellationToken)
            .ConfigureAwait(false);
        if (rascunho is null)
        {
            return null;
        }

        DateTimeOffset agora = timeProvider.GetUtcNow();
        if (rascunho.Expirou(agora))
        {
            // A condição viaja junto com o DELETE: se o próprio dono gravou de outra aba entre
            // esta leitura e agora, o prazo foi renovado e a linha não é mais a vencida que
            // este veredito descreve.
            await rascunhoRepository.ApagarSeVencidoAsync(rascunho.Id, agora, cancellationToken).ConfigureAwait(false);
            return null;
        }

        using JsonDocument documento = JsonDocument.Parse(rascunho.Conteudo);
        return new RascunhoDaPublicacaoDto(rascunho.Versao, documento.RootElement.Clone(), rascunho.SalvoEm);
    }
}
