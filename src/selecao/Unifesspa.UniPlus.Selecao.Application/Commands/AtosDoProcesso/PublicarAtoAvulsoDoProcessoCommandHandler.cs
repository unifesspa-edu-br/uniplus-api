namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Publica um ato avulso do processo: confere tudo o que o Publicações recusaria depois, grava o
/// registro do ato do processo e enfileira a requisição de registro, na mesma transação (ADR-0108).
/// </summary>
/// <remarks>
/// <b>Por que conferir aqui.</b> O Publicações só responde o sucesso; a recusa vai para a fila de
/// mensagens que falharam. Cada regra que o catálogo ou o registro central aplicariam é antecipada com
/// erro nomeado, antes de qualquer escrita. O que o documento avulso e a publicação de um item do edital
/// partilham — gravar o registro e enfileirar a requisição — está em <see cref="EnvioDoAtoDoProcesso"/>.
/// </remarks>
public static class PublicarAtoAvulsoDoProcessoCommandHandler
{
    public static async Task<(Result<AtoPublicadoDto> Resposta, IEnumerable<object> Eventos)> Handle(
        PublicarAtoAvulsoDoProcessoCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        ITipoAtoPublicadoReader tipoDeAtoReader,
        IVagaDeLinhagemReader vagaDeLinhagemReader,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(atoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(documentoAPublicarRepository);
        ArgumentNullException.ThrowIfNull(tipoDeAtoReader);
        ArgumentNullException.ThrowIfNull(vagaDeLinhagemReader);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        // A forma da correção é do agregado e sai antes de qualquer leitura ou trava (ADR-0125).
        Result correcaoBemFormada = AtoDoProcesso.ValidarCorrecao(command.AtoCorrigidoId, command.MotivoDaCorrecao);
        if (correcaoBemFormada.IsFailure)
        {
            return (Result<AtoPublicadoDto>.ValidationFailure(correcaoBemFormada.Errors), []);
        }

        // As conferências abaixo leem para depois inserir (vaga do tipo único, documento já usado):
        // sem serializar as publicações do mesmo processo, duas requisições passam juntas e as duas
        // gravam. O lock cai com a transação, depois que o registro do ato já é visível à seguinte.
        await processoSeletivoRepository
            .TravarAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        Result<ProcessoDivulgadoConferido> conferenciaDoProcesso = await EnvioDoAtoDoProcesso
            .ConferirProcessoDivulgadoAsync(processoSeletivoRepository, certameDivulgadoRepository, command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDoProcesso.IsFailure)
        {
            return Recusa(conferenciaDoProcesso.Error!);
        }

        Result<TipoAtoPublicadoView> conferenciaDoTipo = await ConferenciaDoTipoDoAtoAvulso
            .ConferirAsync(tipoDeAtoReader, command.TipoAtoCodigo, command.DataPublicacao, cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDoTipo.IsFailure)
        {
            return Recusa(conferenciaDoTipo.Error!);
        }

        TipoAtoPublicadoView tipo = conferenciaDoTipo.Value!;

        Result conferenciaDoAtoExigido = ConferenciaDoTipoDoAtoAvulso.ExigeAtoCorrigido(tipo, command.AtoCorrigidoId);
        if (conferenciaDoAtoExigido.IsFailure)
        {
            return Recusa(conferenciaDoAtoExigido.Error!);
        }

        Result<IReadOnlyList<Guid>> conferenciaDoCorrigido = await ConferenciaDoAtoCorrigido
            .ConferirAsync(atoDoProcessoRepository, processoSeletivoRepository, vagaDeLinhagemReader, command.ProcessoSeletivoId, command.AtoCorrigidoId, cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDoCorrigido.IsFailure)
        {
            return Recusa(conferenciaDoCorrigido.Error!);
        }

        Result conferenciaDoItem = await ConferenciaDoAtoCorrigido
            .NaoEhItemDoEditalAsync(atoDoProcessoRepository, conferenciaDoCorrigido.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDoItem.IsFailure)
        {
            return Recusa(conferenciaDoItem.Error!);
        }

        // O ato que corrige integra a linhagem do corrigido, e a vaga do objeto é da linhagem.
        Result conferenciaDaVaga = await ConferenciaDoTipoDeAto
            .VagaDoObjetoAsync(vagaDeLinhagemReader, tipo, command.ProcessoSeletivoId, conferenciaDoCorrigido.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDaVaga.IsFailure)
        {
            return Recusa(conferenciaDaVaga.Error!);
        }

        Result conferenciaDaVagaLocal = await ConferenciaDoTipoDoAtoAvulso
            .VagaNoRegistroLocalAsync(atoDoProcessoRepository, tipo, command.ProcessoSeletivoId, conferenciaDoCorrigido.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDaVagaLocal.IsFailure)
        {
            return Recusa(conferenciaDaVagaLocal.Error!);
        }

        Result<DocumentosDoAtoConferidos> conferenciaDosDocumentos = await ConferenciaDosDocumentosDoAto
            .ConferirAsync(documentoAPublicarRepository, atoDoProcessoRepository, command.ProcessoSeletivoId, command.DocumentoPrincipalId, command.Anexos ?? [], cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDosDocumentos.IsFailure)
        {
            return Recusa(conferenciaDosDocumentos.Error!);
        }

        return await EnvioDoAtoDoProcesso
            .EnviarAsync(
                new AtoAEnviar(
                    command.ProcessoSeletivoId,
                    conferenciaDoProcesso.Value!,
                    tipo,
                    command.Numero,
                    command.DataPublicacao,
                    command.Assinante,
                    conferenciaDosDocumentos.Value!,
                    command.AtoCorrigidoId,
                    command.MotivoDaCorrecao),
                atoDoProcessoRepository,
                unitOfWork,
                timeProvider,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static (Result<AtoPublicadoDto> Resposta, IEnumerable<object> Eventos) Recusa(DomainError erro) =>
        (Result<AtoPublicadoDto>.Failure(erro), []);
}
