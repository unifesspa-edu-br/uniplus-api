namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;

using DTOs;

using ProcessosSeletivos;

using Queries.ProdutosPrevistos;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Publica um item que a versão vigente do edital declara: confere tudo o que o Publicações recusaria
/// depois e o que o edital exige, grava o registro do ato do processo com o item e enfileira a requisição
/// de registro, na mesma transação (ADR-0108).
/// </summary>
/// <remarks>
/// <para>
/// O item só conta como publicado quando o Publicações confirma o registro: até lá o ato fica pendente, e
/// nem a segunda publicação do item nem o definitivo que espera o preliminar passam por ele.
/// </para>
/// <para>
/// A primeira publicação do item é a que não corrige ato algum, e o índice único por processo e item a
/// garante contra a corrida. A correção leva o mesmo item e aponta a publicação corrigida, e por isso não
/// esbarra na unicidade; a versão que vale é a cabeça da cadeia de correções.
/// </para>
/// </remarks>
public static class PublicarProdutoDoEditalCommandHandler
{
    public static async Task<(Result<AtoPublicadoDto> Resposta, IEnumerable<object> Eventos)> Handle(
        PublicarProdutoDoEditalCommand command,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IRegistroCodecsEnvelope registroCodecs,
        ITipoAtoPublicadoReader tipoDeAtoReader,
        IVagaDeLinhagemReader vagaDeLinhagemReader,
        IUserContext userContext,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(atoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(documentoAPublicarRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(tipoDeAtoReader);
        ArgumentNullException.ThrowIfNull(vagaDeLinhagemReader);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        // A forma da correção é do agregado e sai antes de qualquer leitura ou trava (ADR-0125).
        Result correcaoBemFormada = AtoDoProcesso.ValidarCorrecao(command.AtoCorrigidoId, command.MotivoDaCorrecao);
        if (correcaoBemFormada.IsFailure)
        {
            return (Result<AtoPublicadoDto>.ValidationFailure(correcaoBemFormada.Errors), []);
        }

        // As conferências abaixo leem para depois inserir (documento já usado, vaga do tipo único, ordem e
        // fechamento do preliminar): sem serializar as publicações do mesmo processo, duas requisições passam
        // juntas e as duas gravam. O lock cai com a transação, depois que o registro do ato já é visível à seguinte.
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

        Result<GrafoVigente> vigente = await GrafoVigenteDoProcesso
            .LerAsync(processoSeletivoRepository, registroCodecs, command.ProcessoSeletivoId, timeProvider.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);
        if (vigente.IsFailure)
        {
            return Recusa(vigente.Error!);
        }

        Domain.ValueObjects.GrafoConfiguracao grafo = vigente.Value!.Grafo;
        Result<ProjecaoDosProdutosPrevistos.Declarado> conferenciaDoProduto =
            ConferenciaDoProdutoDoEdital.Declarado(grafo, command.ProdutoDoEditalId);
        if (conferenciaDoProduto.IsFailure)
        {
            return Recusa(conferenciaDoProduto.Error!);
        }

        ProjecaoDosProdutosPrevistos.Declarado produto = conferenciaDoProduto.Value!;

        Result<TipoAtoPublicadoView> conferenciaDoTipo = await ConferenciaDoProdutoDoEdital
            .TipoAsync(tipoDeAtoReader, produto, vigente.Value!.TiposDeAto, command.DataPublicacao, cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDoTipo.IsFailure)
        {
            return Recusa(conferenciaDoTipo.Error!);
        }

        TipoAtoPublicadoView tipo = conferenciaDoTipo.Value!;

        IReadOnlyList<AtoDoProcesso> atos = await atoDoProcessoRepository
            .ListarDoProcessoAsync(command.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        CadeiasDosProdutosPrevistos cadeias = CadeiasDosProdutosPrevistos.De(atos);

        Result<IReadOnlyList<Guid>> conferenciaDoCorrigido = await ConferirPublicacaoOuCorrecaoAsync(
            command, produto, grafo, cadeias, atoDoProcessoRepository, processoSeletivoRepository, vagaDeLinhagemReader, cancellationToken)
            .ConfigureAwait(false);
        if (conferenciaDoCorrigido.IsFailure)
        {
            return Recusa(conferenciaDoCorrigido.Error!);
        }

        Result<string?> conferenciaDaPolitica = ConferenciaDoProdutoDoEdital
            .Politica(tipo, command.PoliticaDeDivulgacaoConfirmada, userContext.UserId, grafo);
        if (conferenciaDaPolitica.IsFailure)
        {
            return Recusa(conferenciaDaPolitica.Error!);
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
                    command.MotivoDaCorrecao,
                    ProdutoDoEditalId: produto.Id,
                    PoliticaConfirmadaPorSub: conferenciaDaPolitica.Value),
                atoDoProcessoRepository,
                unitOfWork,
                timeProvider,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <returns>Os atos da linhagem a que o novo ato passa a pertencer; vazio na primeira publicação.</returns>
    private static async Task<Result<IReadOnlyList<Guid>>> ConferirPublicacaoOuCorrecaoAsync(
        PublicarProdutoDoEditalCommand command,
        ProjecaoDosProdutosPrevistos.Declarado produto,
        Domain.ValueObjects.GrafoConfiguracao grafo,
        CadeiasDosProdutosPrevistos cadeias,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IVagaDeLinhagemReader vagaDeLinhagemReader,
        CancellationToken cancellationToken)
    {
        if (command.AtoCorrigidoId is not { } atoCorrigidoId)
        {
            Result primeira = ConferenciaDoProdutoDoEdital.PrimeiraPublicacao(produto, grafo, cadeias);
            return primeira.IsFailure
                ? Result<IReadOnlyList<Guid>>.Failure(primeira.Error!)
                : Result<IReadOnlyList<Guid>>.Success([]);
        }

        Result<IReadOnlyList<Guid>> corrigido = await ConferenciaDoAtoCorrigido
            .ConferirAsync(atoDoProcessoRepository, processoSeletivoRepository, vagaDeLinhagemReader, command.ProcessoSeletivoId, atoCorrigidoId, cancellationToken)
            .ConfigureAwait(false);
        if (corrigido.IsFailure)
        {
            return corrigido;
        }

        Result correcao = ConferenciaDoProdutoDoEdital.Correcao(produto, atoCorrigidoId, grafo, cadeias);
        return correcao.IsFailure ? Result<IReadOnlyList<Guid>>.Failure(correcao.Error!) : corrigido;
    }

    private static (Result<AtoPublicadoDto> Resposta, IEnumerable<object> Eventos) Recusa(DomainError erro) =>
        (Result<AtoPublicadoDto>.Failure(erro), []);
}
