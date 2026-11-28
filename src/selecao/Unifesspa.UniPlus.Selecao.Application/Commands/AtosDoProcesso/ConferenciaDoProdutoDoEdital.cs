namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using ProcessosSeletivos;

using Queries.ProdutosPrevistos;

using Services;


using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

/// <summary>O item declarado pela versão vigente do edital e a configuração em que ele foi lido.</summary>
internal sealed record ItemDeclaradoPelaVersaoVigente(ConfiguracaoVigente Vigente, ProjecaoDosProdutosPrevistos.Declarado Produto);

/// <summary>
/// O que a conferência do item concluiu, em três famílias de causas: as do item, as da política de divulgação e as da
/// correção, e os valores conferidos que o ato registra quando nenhuma causa os impede.
/// </summary>
/// <param name="DoItem">As causas do item: declaração, tipo e primeira publicação; sem campo, porque o item vem da rota.</param>
/// <param name="DaPolitica">As causas da confirmação da política de divulgação.</param>
/// <param name="DaCorrecao">As causas do ato corrigido.</param>
/// <param name="Item">A identidade do item no cronograma; nulo quando o item não foi conferido.</param>
/// <param name="Tipo">O tipo do item; nulo quando o tipo foi recusado ou não foi lido.</param>
/// <param name="ContaId">Quem confirmou a política de divulgação, quando ela foi exigida.</param>
internal sealed record ProdutoDoEditalConferido(
    IReadOnlyList<FieldError> DoItem,
    IReadOnlyList<FieldError> DaPolitica,
    IReadOnlyList<FieldError> DaCorrecao,
    IdentidadeDoItemDoEdital? Item = null,
    TipoAtoPublicadoView? Tipo = null,
    string? ContaId = null)
{
    public static ProdutoDoEditalConferido NaoConsultado { get; } = new([], [], []);
}

/// <summary>
/// Confere, antes de publicar o ato, que o item pedido é um dos que a versão vigente do edital declara e
/// que ele pode sair agora: primeira publicação do item, ordem do preliminar para o definitivo, correção
/// de uma publicação do próprio item e confirmação da política de divulgação.
/// </summary>
/// <remarks>
/// <para>
/// A situação e o impedimento do item são os mesmos que a consulta dos itens previstos mostra, e o
/// pareamento entre preliminar e definitivo vem de <see cref="PreliminaresDaMateria"/>: o que a tela diz
/// que não pode sair é o que o comando recusa. Só a ordem do preliminar para o definitivo é conferida
/// aqui; o prazo de recurso e os julgamentos não, porque o sistema ainda não os controla.
/// </para>
/// <para>
/// O tipo vem do item, e o administrador não o escolhe. Nenhum código compara o tipo com um literal: a versão
/// publicada do edital diz se ele congela configuração, se é resultado e se é único, como o catálogo dizia na
/// publicação (ADR-0103). Mudar a classificação no catálogo depois não muda o que vale para o item.
/// </para>
/// </remarks>
public sealed class ConferenciaDoProdutoDoEdital(
    ConfiguracaoVigenteDoProcesso grafos,
    ConferenciaDoTipoDeAto tipos,
    IAtoDoProcessoRepository atos,
    ConferenciaDoAtoCorrigido correcoes,
    IUserContext usuario)
{
    internal async Task<ProdutoDoEditalConferido> ConferirAsync(
        PublicarProdutoDoEditalCommand command, CorrecaoPedida correcao, InstanteDoComando instante, CancellationToken cancellationToken)
    {
        IReadOnlyList<FieldError> doAtoCorrigido = await ConferirAtoCorrigidoAsync(command.ProcessoSeletivoId, correcao, cancellationToken).ConfigureAwait(false);
        Result<ItemDeclaradoPelaVersaoVigente> item = await LerItemAsync(command, instante, cancellationToken).ConfigureAwait(false);

        return item.IsFailure
            ? new(item.Errors, [], doAtoCorrigido)
            : await ConferirItemAsync(command, correcao, instante, item.Value!, doAtoCorrigido, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<ItemDeclaradoPelaVersaoVigente>> LerItemAsync(
        PublicarProdutoDoEditalCommand command, InstanteDoComando instante, CancellationToken cancellationToken)
    {
        Result<ConfiguracaoVigente> vigente = await grafos.LerAsync(command.ProcessoSeletivoId, instante.Instante, cancellationToken).ConfigureAwait(false);
        if (vigente.IsFailure)
        {
            return Result<ItemDeclaradoPelaVersaoVigente>.ValidationFailure(vigente.Errors);
        }

        Result<ProjecaoDosProdutosPrevistos.Declarado> produto = Declarado(vigente.Value!.Configuracao, command.ProdutoDoEditalId);

        return produto.IsFailure
            ? Result<ItemDeclaradoPelaVersaoVigente>.ValidationFailure(produto.Errors)
            : Result<ItemDeclaradoPelaVersaoVigente>.Success(new(vigente.Value, produto.Value!));
    }

    private async Task<ProdutoDoEditalConferido> ConferirItemAsync(
        PublicarProdutoDoEditalCommand command, CorrecaoPedida correcao, InstanteDoComando instante,
        ItemDeclaradoPelaVersaoVigente item, IReadOnlyList<FieldError> doAtoCorrigido, CancellationToken cancellationToken)
    {
        Guid processoId = command.ProcessoSeletivoId;
        ConfiguracaoPublicada grafo = item.Vigente.Configuracao;
        Result<TipoAtoPublicadoView> tipo = await TipoAsync(item.Produto, item.Vigente.TiposDeAto, instante.Dia, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<FieldError> primeiraPublicacao = await ConferirPrimeiraPublicacaoAsync(processoId, correcao, item.Produto, grafo, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<FieldError> correcaoDoItem = await ConferirCorrecaoDoItemAsync(processoId, correcao, doAtoCorrigido.Count > 0, item.Produto, grafo, cancellationToken).ConfigureAwait(false);
        PoliticaConferida politica = tipo.IsSuccess
            ? ConfirmacaoDaPoliticaDeDivulgacao.Conferir(tipo.Value!, command.PoliticaDeDivulgacaoConfirmada, usuario.UserId, grafo)
            : PoliticaConferida.NaoConferida;

        return new([.. tipo.Errors, .. primeiraPublicacao], politica.Recusas, [.. doAtoCorrigido, .. correcaoDoItem], item.Produto.Item, tipo.Value, politica.ContaId);
    }

    /// <summary>O ato corrigido é conferido sem depender do item: a conferência não lê o que o item declara.</summary>
    private async Task<IReadOnlyList<FieldError>> ConferirAtoCorrigidoAsync(
        Guid processoId, CorrecaoPedida correcao, CancellationToken cancellationToken)
    {
        if (correcao.AtoCorrigidoId is not { } corrigido)
        {
            return [];
        }

        Result<IReadOnlyList<Guid>> conferido = await correcoes.ConferirAsync(processoId, corrigido, cancellationToken).ConfigureAwait(false);

        return conferido.Errors;
    }

    private async Task<IReadOnlyList<FieldError>> ConferirPrimeiraPublicacaoAsync(
        Guid processoId, CorrecaoPedida correcao, ProjecaoDosProdutosPrevistos.Declarado produto,
        ConfiguracaoPublicada grafo, CancellationToken cancellationToken)
    {
        if (correcao.Situacao != SituacaoDaCorrecaoPedida.SemCorrecao)
        {
            return [];
        }

        return PrimeiraPublicacao(produto, grafo, await CadeiasAsync(processoId, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>O ato corrigido ser do item e o preliminar ainda estar aberto só se confere com o ato corrigido aceito.</summary>
    private async Task<IReadOnlyList<FieldError>> ConferirCorrecaoDoItemAsync(
        Guid processoId, CorrecaoPedida correcao, bool atoCorrigidoRecusado, ProjecaoDosProdutosPrevistos.Declarado produto,
        ConfiguracaoPublicada grafo, CancellationToken cancellationToken)
    {
        if (correcao.AtoCorrigidoId is not { } corrigido || atoCorrigidoRecusado)
        {
            return [];
        }

        return Correcao(produto, corrigido, grafo, await CadeiasAsync(processoId, cancellationToken).ConfigureAwait(false));
    }

    private async Task<CadeiasDosProdutosPrevistos> CadeiasAsync(Guid processoId, CancellationToken cancellationToken) =>
        CadeiasDosProdutosPrevistos.De(await atos.ListarDoProcessoAsync(processoId, cancellationToken).ConfigureAwait(false));

    private static Result<ProjecaoDosProdutosPrevistos.Declarado> Declarado(ConfiguracaoPublicada grafo, Guid produtoId) =>
        ProjecaoDosProdutosPrevistos.ProdutoDeclarado(grafo, produtoId) is { } produto
            ? Result<ProjecaoDosProdutosPrevistos.Declarado>.Success(produto)
            : Result<ProjecaoDosProdutosPrevistos.Declarado>.Failure(new DomainError(
                AtoDoProcessoErrorCodes.ProdutoNaoDeclarado,
                "O item não consta na versão vigente do edital. Só se publica o que o edital declara; se for preciso publicar outro, retifique o edital."));

    /// <summary>
    /// O tipo do item no dia da publicação: vigora no catálogo e não congela configuração. O catálogo diz se o
    /// tipo existe e o seu nome; se ele congela, se é resultado e se é único, diz a versão publicada do edital.
    /// </summary>
    private async Task<Result<TipoAtoPublicadoView>> TipoAsync(
        ProjecaoDosProdutosPrevistos.Declarado produto,
        IReadOnlyDictionary<string, TipoDeAtoCongelado> tiposCongelados,
        DateOnly diaDaPublicacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(produto);
        ArgumentNullException.ThrowIfNull(tiposCongelados);

        Result<TipoAtoPublicadoView> vigente = await tipos
            .VigenteAsync(produto.AtoCodigo, diaDaPublicacao, cancellationToken)
            .ConfigureAwait(false);
        if (vigente.IsFailure)
        {
            return vigente;
        }

        return Classificar(vigente.Value!, produto, tiposCongelados);
    }

    private static Result<TipoAtoPublicadoView> Classificar(
        TipoAtoPublicadoView vigente, ProjecaoDosProdutosPrevistos.Declarado produto,
        IReadOnlyDictionary<string, TipoDeAtoCongelado> tiposCongelados)
    {
        TipoDeAtoCongelado congelado = ClassificacaoCongelada(produto, tiposCongelados);
        TipoAtoPublicadoView tipo = vigente with
        {
            CongelaConfiguracao = congelado.CongelaConfiguracao,
            UnicoPorObjeto = congelado.UnicoPorObjeto,
            EhResultado = congelado.EhResultado,
        };
        return tipo.CongelaConfiguracao
            ? Result<TipoAtoPublicadoView>.Failure(new DomainError(
                AtoDoProcessoErrorCodes.TipoCongelaConfiguracao,
                "O tipo de ato congela configuração e só se publica com a publicação ou a retificação do edital — o item do edital não altera a configuração do processo."))
            : Result<TipoAtoPublicadoView>.Success(tipo);
    }

    private static TipoDeAtoCongelado ClassificacaoCongelada(
        ProjecaoDosProdutosPrevistos.Declarado produto, IReadOnlyDictionary<string, TipoDeAtoCongelado> tiposCongelados) =>
        tiposCongelados.TryGetValue(produto.AtoCodigo, out TipoDeAtoCongelado? congelado)
            ? congelado
            : throw new InvalidOperationException(
                "A versão publicada declara o item sem congelar a classificação do tipo; o envelope lido garante uma entrada por tipo declarado.");

    /// <summary>A primeira publicação do item: ele está aberto e, sendo definitivo, o preliminar da matéria já saiu.</summary>
    private static IReadOnlyList<FieldError> PrimeiraPublicacao(
        ProjecaoDosProdutosPrevistos.Declarado produto,
        ConfiguracaoPublicada configuracaoPublicada,
        CadeiasDosProdutosPrevistos cadeias)
    {
        ArgumentNullException.ThrowIfNull(produto);
        ArgumentNullException.ThrowIfNull(configuracaoPublicada);
        ArgumentNullException.ThrowIfNull(cadeias);
        SituacaoDoProdutoPrevisto situacao = ProjecaoDosProdutosPrevistos.SituacaoDe(cadeias.PrimeiraPublicacaoDe(produto.Item));

        ImpedimentoDoProdutoDto? impedimento = ProjecaoDosProdutosPrevistos
            .ImpedimentoDe(produto, situacao, configuracaoPublicada, cadeias);

        return impedimento is null
            ? []
            : [new FieldError(null, new DomainError(CodigoDoImpedimento(CatalogoDe<ImpedimentoDoProduto>.Obter(impedimento.Motivo.Codigo)), impedimento.Mensagem))];
    }

    /// <summary>
    /// A correção de uma publicação do item: o ato corrigido é do item e, sendo o item um preliminar, o
    /// definitivo que fecha o ciclo dele ainda não saiu (UNI-REQ-0162).
    /// </summary>
    private static IReadOnlyList<FieldError> Correcao(
        ProjecaoDosProdutosPrevistos.Declarado produto,
        Guid atoCorrigidoId,
        ConfiguracaoPublicada configuracaoPublicada,
        CadeiasDosProdutosPrevistos cadeias)
    {
        ArgumentNullException.ThrowIfNull(produto);
        ArgumentNullException.ThrowIfNull(configuracaoPublicada);
        ArgumentNullException.ThrowIfNull(cadeias);
        if (!cadeias.AtosDe([produto.Item]).Any(a => a.Id == atoCorrigidoId))
        {
            return [RecusaDoAtoCorrigido(AtoDoProcessoErrorCodes.AtoCorrigidoNaoEhDoProduto,
                "O ato não é uma publicação deste item do edital, e a correção de um item corrige a publicação dele.")];
        }

        return PreliminarFechado(produto, configuracaoPublicada, cadeias)
            ? [RecusaDoAtoCorrigido(AtoDoProcessoErrorCodes.PreliminarFechadoPeloDefinitivo,
                "O resultado preliminar não pode mais ser corrigido: o definitivo da mesma matéria já foi publicado e fechou o ciclo de recurso. Corrija o resultado definitivo, por errata ou retificação do ato.")]
            : [];
    }

    private static bool PreliminarFechado(
        ProjecaoDosProdutosPrevistos.Declarado produto, ConfiguracaoPublicada grafo, CadeiasDosProdutosPrevistos cadeias)
    {
        Dictionary<Guid, IdentidadeDoItemDoEdital> itens = IdentidadeDoItemDoEdital.Dos(grafo.Cronograma.CronogramaFases, grafo.Avaliacao.Etapas);
        return produto.Papel == PapelDaPublicacaoPrevista.Preliminar
            && PreliminaresDaMateria.DefinitivosQueFecham(grafo, produto.Id).Any(d => cadeias.PrimeiraPublicacaoDe(itens[d]) is not null);
    }

    private static FieldError RecusaDoAtoCorrigido(string codigo, string mensagem) =>
        new(AtoDoProcesso.CampoDoAtoCorrigido, new DomainError(codigo, mensagem));

    private static string CodigoDoImpedimento(ImpedimentoDoProduto motivo) =>
        motivo switch
        {
            ImpedimentoDoProduto.JaPublicado => AtoDoProcessoErrorCodes.ProdutoJaPublicado,
            ImpedimentoDoProduto.PreliminarNaoPublicado => AtoDoProcessoErrorCodes.PreliminarNaoPublicado,
            ImpedimentoDoProduto.TipoDeAtoNaoVigente => "ProcessoSeletivo.TipoDeAtoSemVersaoVigente",
            _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, "Impedimento do item sem código de erro."),
        };
}
