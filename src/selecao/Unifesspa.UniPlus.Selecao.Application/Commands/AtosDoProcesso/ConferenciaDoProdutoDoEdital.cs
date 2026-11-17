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
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

using ProdutoConferido = (Unifesspa.UniPlus.Selecao.Domain.ValueObjects.IdentidadeDoItemDoEdital Item, Unifesspa.UniPlus.Publicacoes.Contracts.TipoAtoPublicadoView Tipo, string? ContaId);

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
    GrafoVigenteDoProcesso grafos,
    ConferenciaDoTipoDeAto tipos,
    IAtoDoProcessoRepository atos,
    ConferenciaDoAtoCorrigido correcoes,
    IUserContext usuario)
{
    internal async Task<Result<ProdutoConferido>> ConferirAsync(
        PublicarProdutoDoEditalCommand command, InstanteDoComando instante, CancellationToken cancellationToken)
    {
        Result<GrafoVigente> vigente = await grafos.LerAsync(command.ProcessoSeletivoId, instante.Instante, cancellationToken).ConfigureAwait(false);
        if (vigente.IsFailure)
        {
            return Result<ProdutoConferido>.Failure(vigente.Error!);
        }
        Result<ProjecaoDosProdutosPrevistos.Declarado> produto = Declarado(vigente.Value!.Grafo, command.ProdutoDoEditalId);
        return produto.IsFailure
            ? Result<ProdutoConferido>.Failure(produto.Error!)
            : await ConferirTipoECadeiaAsync(command, instante, vigente.Value, produto.Value!, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<ProdutoConferido>> ConferirTipoECadeiaAsync(
        PublicarProdutoDoEditalCommand command, InstanteDoComando instante, GrafoVigente vigente,
        ProjecaoDosProdutosPrevistos.Declarado produto, CancellationToken cancellationToken)
    {
        Result<TipoAtoPublicadoView> tipo = await TipoAsync(produto, vigente.TiposDeAto, instante.Dia, cancellationToken).ConfigureAwait(false);
        if (tipo.IsFailure)
        {
            return Result<ProdutoConferido>.Failure(tipo.Error!);
        }
        IReadOnlyList<AtoDoProcesso> publicados = await atos.ListarDoProcessoAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);
        return await ConferirPublicacaoEPoliticaAsync(command, produto, vigente.Grafo, tipo.Value!, CadeiasDosProdutosPrevistos.De(publicados), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result<ProdutoConferido>> ConferirPublicacaoEPoliticaAsync(
        PublicarProdutoDoEditalCommand command, ProjecaoDosProdutosPrevistos.Declarado produto,
        GrafoConfiguracao grafo, TipoAtoPublicadoView tipo, CadeiasDosProdutosPrevistos cadeias, CancellationToken cancellationToken)
    {
        Result publicacao = command.AtoCorrigidoId is { } corrigido
            ? await ConferirCorrecaoAsync(command.ProcessoSeletivoId, corrigido, produto, grafo, cadeias, cancellationToken).ConfigureAwait(false)
            : PrimeiraPublicacao(produto, grafo, cadeias);
        if (publicacao.IsFailure)
        {
            return Result<ProdutoConferido>.Failure(publicacao.Error!);
        }
        Result<string?> politica = Politica(tipo, command.PoliticaDeDivulgacaoConfirmada, usuario.UserId, grafo);
        return politica.IsFailure
            ? Result<ProdutoConferido>.Failure(politica.Error!)
            : Result<ProdutoConferido>.Success((produto.Item, tipo, politica.Value));
    }

    private async Task<Result> ConferirCorrecaoAsync(
        Guid processoId, Guid corrigido, ProjecaoDosProdutosPrevistos.Declarado produto,
        GrafoConfiguracao grafo, CadeiasDosProdutosPrevistos cadeias, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<Guid>> correcao = await correcoes.ConferirAsync(processoId, corrigido, cancellationToken).ConfigureAwait(false);
        return correcao.IsFailure ? Result.Failure(correcao.Error!) : Correcao(produto, corrigido, grafo, cadeias);
    }

    private static Result<ProjecaoDosProdutosPrevistos.Declarado> Declarado(GrafoConfiguracao grafo, Guid produtoId) =>
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
    private static Result PrimeiraPublicacao(
        ProjecaoDosProdutosPrevistos.Declarado produto,
        GrafoConfiguracao grafo,
        CadeiasDosProdutosPrevistos cadeias)
    {
        ArgumentNullException.ThrowIfNull(produto);
        ArgumentNullException.ThrowIfNull(grafo);
        ArgumentNullException.ThrowIfNull(cadeias);
        SituacaoDoProdutoPrevisto situacao = ProjecaoDosProdutosPrevistos.SituacaoDe(cadeias.PrimeiraPublicacaoDe(produto.Item));

        // O tipo vigente já foi conferido no dia da publicação; a consulta não escolhe esse dia.
        ImpedimentoDoProdutoDto? impedimento = ProjecaoDosProdutosPrevistos
            .ImpedimentoDe(produto, situacao, grafo, cadeias, tipoVigente: true);

        return impedimento is null
            ? Result.Success()
            : Result.Failure(new DomainError(CodigoDoImpedimento(impedimento.Motivo), impedimento.Mensagem));
    }

    /// <summary>
    /// A correção de uma publicação do item: o ato corrigido é do item e, sendo o item um preliminar, o
    /// definitivo que fecha o ciclo dele ainda não saiu (UNI-REQ-0162).
    /// </summary>
    private static Result Correcao(
        ProjecaoDosProdutosPrevistos.Declarado produto,
        Guid atoCorrigidoId,
        GrafoConfiguracao grafo,
        CadeiasDosProdutosPrevistos cadeias)
    {
        ArgumentNullException.ThrowIfNull(produto);
        ArgumentNullException.ThrowIfNull(grafo);
        ArgumentNullException.ThrowIfNull(cadeias);
        if (!cadeias.AtosDe([produto.Item]).Any(a => a.Id == atoCorrigidoId))
        {
            return Result.Failure(new DomainError(
                AtoDoProcessoErrorCodes.AtoCorrigidoNaoEhDoProduto,
                "O ato não é uma publicação deste item do edital, e a correção de um item corrige a publicação dele."));
        }

        return PreliminarFechado(produto, grafo, cadeias)
            ? Result.Failure(new DomainError(AtoDoProcessoErrorCodes.PreliminarFechadoPeloDefinitivo,
                "O resultado preliminar não pode mais ser corrigido: o definitivo da mesma matéria já foi publicado e fechou o ciclo de recurso. Corrija o resultado definitivo, por errata ou retificação do ato."))
            : Result.Success();
    }

    private static bool PreliminarFechado(
        ProjecaoDosProdutosPrevistos.Declarado produto, GrafoConfiguracao grafo, CadeiasDosProdutosPrevistos cadeias)
    {
        Dictionary<Guid, IdentidadeDoItemDoEdital> itens = IdentidadeDoItemDoEdital.Dos(grafo.CronogramaFases, grafo.Etapas);
        return produto.Papel == PapelProdutoFase.Preliminar
            && PreliminaresDaMateria.DefinitivosQueFecham(grafo, produto.Id).Any(d => cadeias.PrimeiraPublicacaoDe(itens[d]) is not null);
    }

    /// <summary>
    /// A confirmação da política de divulgação, exigida quando o tipo é resultado. Devolve o contaId de quem
    /// confirmou, para ficar no registro, ou nulo quando a confirmação não foi pedida.
    /// </summary>
    private static Result<string?> Politica(
        TipoAtoPublicadoView tipo,
        bool confirmada,
        string? contaId,
        GrafoConfiguracao grafo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        ArgumentNullException.ThrowIfNull(grafo);

        if (!tipo.EhResultado)
        {
            return Result<string?>.Success(null);
        }

        return !confirmada ? PoliticaNaoConfirmada(grafo) : IdentificarQuemConfirmou(contaId);
    }

    private static Result<string?> PoliticaNaoConfirmada(GrafoConfiguracao grafo)
    {
        ConfiguracaoDivulgacaoDto politica = ProjecaoDosProdutosPrevistos.PoliticaDeDivulgacaoDaVersao(grafo);
        string campos = string.Join(", ", ConfiguracaoDivulgacao.CamposPermitidos
            .Where(c => politica.CamposPublicos.Contains(c.Codigo, StringComparer.Ordinal))
            .Select(static c => c.Nome.ToLowerInvariant()));

        return Result<string?>.Failure(new DomainError(
            AtoDoProcessoErrorCodes.PoliticaDeDivulgacaoNaoConfirmada,
            $"O tipo de ato é um resultado, e publicá-lo exige confirmar que o PDF segue a política de divulgação do certame, a da versão vigente: campos públicos — {campos}. O sistema não confere o conteúdo do PDF."));
    }

    private static Result<string?> IdentificarQuemConfirmou(string? contaId)
    {
        return string.IsNullOrWhiteSpace(contaId)
            ? Result<string?>.Failure(new DomainError(
                AtoDoProcessoErrorCodes.UsuarioNaoIdentificado,
                "O token não traz o claim sub, e a confirmação da política de divulgação precisa registrar quem a fez."))
            : Result<string?>.Success(contaId);
    }

    private static string CodigoDoImpedimento(ImpedimentoDoProduto motivo) =>
        motivo switch
        {
            ImpedimentoDoProduto.JaPublicado => AtoDoProcessoErrorCodes.ProdutoJaPublicado,
            ImpedimentoDoProduto.PreliminarNaoPublicado => AtoDoProcessoErrorCodes.PreliminarNaoPublicado,
            ImpedimentoDoProduto.TipoDeAtoNaoVigente => "ProcessoSeletivo.TipoDeAtoSemVersaoVigente",
            _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, "Impedimento do item sem código de erro."),
        };
}
