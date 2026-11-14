namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using ProcessosSeletivos;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Publica um ato avulso do processo: confere o tipo, a correção e os documentos, grava o registro do ato
/// do processo e o entrega à porta de escrita do registro central, na mesma transação (ADR-0170).
/// </summary>
/// <remarks>
/// <b>Quem recusa o quê.</b> A Seleção confere o que é dela, antes de qualquer escrita. O que é do registro
/// central — a vaga do objeto e a cadeia de retificação — é recusado pela porta, na mesma transação, com erro
/// nomeado e nada gravado. O que o documento avulso e a publicação de um item do edital partilham — gravar o
/// registro e entregá-lo à porta — está em <see cref="EnvioDoAtoDoProcesso"/>.
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
        IRegistroDeAtoNormativo registroDeAtos,
        IUnitOfWork unitOfWork,
        LeituraDoInstanteDaPublicacao leituraDoInstante,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(atoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(documentoAPublicarRepository);
        ArgumentNullException.ThrowIfNull(tipoDeAtoReader);
        ArgumentNullException.ThrowIfNull(registroDeAtos);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(leituraDoInstante);

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

        Result<InstanteDaPublicacao> publicacao = leituraDoInstante.Ler();
        if (publicacao.IsFailure)
        {
            return Recusa(publicacao.Error!);
        }

        Result<TipoAtoPublicadoView> conferenciaDoTipo = await ConferenciaDoTipoDoAtoAvulso
            .ConferirAsync(tipoDeAtoReader, command.TipoAtoCodigo, publicacao.Value.Dia, cancellationToken)
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
            .ConferirAsync(atoDoProcessoRepository, processoSeletivoRepository, command.ProcessoSeletivoId, command.AtoCorrigidoId, cancellationToken)
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
                    command.DataDoAto,
                    publicacao.Value.Instante,
                    command.Assinante,
                    conferenciaDosDocumentos.Value!,
                    command.AtoCorrigidoId,
                    command.MotivoDaCorrecao),
                atoDoProcessoRepository,
                registroDeAtos,
                unitOfWork,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static (Result<AtoPublicadoDto> Resposta, IEnumerable<object> Eventos) Recusa(DomainError erro) =>
        (Result<AtoPublicadoDto>.Failure(erro), []);
}
