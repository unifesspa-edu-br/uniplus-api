namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Publica um ato avulso do processo: confere o tipo, a correção e os documentos, grava o registro do ato
/// do processo e o entrega à porta de escrita do registro central, na mesma transação (ADR-0170).
/// </summary>
/// <remarks>
/// <b>Quem recusa o quê.</b> A Seleção confere o que é dela, antes de qualquer escrita. O que é do registro
/// central — a vaga do objeto e a cadeia de retificação — é recusado pela porta, na mesma transação, com erro
/// nomeado e nada gravado. O que o documento avulso e a publicação de um item do edital partilham — gravar o
/// registro e entregá-lo à porta — está em <see cref="PublicacaoDoAtoDoProcesso"/>.
/// </remarks>
public static class PublicarAtoAvulsoDoProcessoCommandHandler
{
    public static async Task<(Result<AtoPublicadoDto> Resposta, IEnumerable<object> Eventos)> Handle(
        PublicarAtoAvulsoDoProcessoCommand command,
        IProcessoSeletivoRepository processos,
        PreparacaoDaPublicacaoDoAtoAvulso preparacao,
        PublicacaoDoAtoDoProcesso publicacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processos);
        ArgumentNullException.ThrowIfNull(preparacao);
        ArgumentNullException.ThrowIfNull(publicacao);
        Result correcao = AtoDoProcesso.ValidarCorrecao(command.AtoCorrigidoId, command.MotivoDaCorrecao);
        if (correcao.IsFailure)
        {
            return (Result<AtoPublicadoDto>.ValidationFailure(correcao.Errors), []);
        }
        await processos.TravarAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        Result<AtoAPublicar> ato = await preparacao.PrepararAsync(command, cancellationToken).ConfigureAwait(false);
        return ato.IsFailure
            ? (Result<AtoPublicadoDto>.ValidationFailure(ato.Errors), [])
            : await publicacao.PublicarAsync(ato.Value!, cancellationToken).ConfigureAwait(false);
    }
}
