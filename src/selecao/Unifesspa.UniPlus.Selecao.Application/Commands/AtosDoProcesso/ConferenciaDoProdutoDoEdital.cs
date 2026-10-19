namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.ValueObjects;

using DTOs;

using ProcessosSeletivos;

using Queries.ProdutosPrevistos;

using Services;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Confere, antes de enviar o ato, que o item pedido é um dos que a versão vigente do edital declara e
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
internal static class ConferenciaDoProdutoDoEdital
{
    public static Result<ProjecaoDosProdutosPrevistos.Declarado> Declarado(GrafoConfiguracao grafo, Guid produtoId) =>
        ProjecaoDosProdutosPrevistos.ProdutoDeclarado(grafo, produtoId) is { } produto
            ? Result<ProjecaoDosProdutosPrevistos.Declarado>.Success(produto)
            : Result<ProjecaoDosProdutosPrevistos.Declarado>.Failure(new DomainError(
                AtoDoProcessoErrorCodes.ProdutoNaoDeclarado,
                $"O item {produtoId} não consta na versão vigente do edital. Só se publica o que o edital declara; se for preciso publicar outro, retifique o edital."));

    /// <summary>
    /// O tipo do item na data do documento: vigora no catálogo e não congela configuração. O catálogo diz se o
    /// tipo existe e o seu nome; se ele congela, se é resultado e se é único, diz a versão publicada do edital.
    /// </summary>
    public static async Task<Result<TipoAtoPublicadoView>> TipoAsync(
        ITipoAtoPublicadoReader reader,
        ProjecaoDosProdutosPrevistos.Declarado produto,
        IReadOnlyDictionary<string, TipoDeAtoCongelado> tiposCongelados,
        DateOnly dataPublicacao,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(produto);
        ArgumentNullException.ThrowIfNull(tiposCongelados);

        Result<TipoAtoPublicadoView> vigente = await ConferenciaDoTipoDeAto
            .VigenteAsync(reader, produto.AtoCodigo, dataPublicacao, cancellationToken)
            .ConfigureAwait(false);
        if (vigente.IsFailure)
        {
            return vigente;
        }

        if (!tiposCongelados.TryGetValue(produto.AtoCodigo, out TipoDeAtoCongelado? congelado))
        {
            throw new InvalidOperationException(
                $"A versão publicada declara o item do tipo '{produto.AtoCodigo}' sem congelar a classificação dele; o envelope lido garante uma entrada por tipo declarado.");
        }

        TipoAtoPublicadoView tipo = vigente.Value! with
        {
            CongelaConfiguracao = congelado.CongelaConfiguracao,
            UnicoPorObjeto = congelado.UnicoPorObjeto,
            EhResultado = congelado.EhResultado,
        };

        return tipo.CongelaConfiguracao
            ? Result<TipoAtoPublicadoView>.Failure(new DomainError(
                AtoDoProcessoErrorCodes.TipoCongelaConfiguracao,
                $"O tipo de ato '{produto.AtoCodigo}' congela configuração e só se publica com a publicação ou a retificação do edital — o item do edital não altera a configuração do processo."))
            : Result<TipoAtoPublicadoView>.Success(tipo);
    }

    /// <summary>A primeira publicação do item: ele está aberto e, sendo definitivo, o preliminar da matéria já saiu.</summary>
    public static Result PrimeiraPublicacao(
        ProjecaoDosProdutosPrevistos.Declarado produto,
        GrafoConfiguracao grafo,
        CadeiasDosProdutosPrevistos cadeias)
    {
        ArgumentNullException.ThrowIfNull(produto);
        ArgumentNullException.ThrowIfNull(grafo);
        ArgumentNullException.ThrowIfNull(cadeias);

        SituacaoDoProdutoPrevisto situacao = ProjecaoDosProdutosPrevistos.SituacaoDe(cadeias.PrimeiraPublicacaoDe(produto.Item));

        // O tipo vigente já foi conferido na data do documento; a data de hoje, que a consulta usa, não manda aqui.
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
    public static Result Correcao(
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
                $"O ato {atoCorrigidoId} não é uma publicação deste item do edital, e a correção de um item corrige a publicação dele."));
        }

        Dictionary<Guid, IdentidadeDoItemDoEdital> itens = IdentidadeDoItemDoEdital.Dos(grafo.CronogramaFases, grafo.Etapas);
        if (produto.Papel == PapelProdutoFase.Preliminar
            && PreliminaresDaMateria.DefinitivosQueFecham(grafo, produto.Id).Any(d => cadeias.PrimeiraPublicacaoDe(itens[d]) is not null))
        {
            return Result.Failure(new DomainError(
                AtoDoProcessoErrorCodes.PreliminarFechadoPeloDefinitivo,
                "O resultado preliminar não pode mais ser corrigido: o definitivo da mesma matéria já foi publicado e fechou o ciclo de recurso. Corrija o resultado definitivo, por errata ou retificação do ato."));
        }

        return Result.Success();
    }

    /// <summary>
    /// A confirmação da política de divulgação, exigida quando o tipo é resultado. Devolve o sub de quem
    /// confirmou, para ficar no registro, ou nulo quando a confirmação não foi pedida.
    /// </summary>
    public static Result<string?> Politica(
        TipoAtoPublicadoView tipo,
        bool confirmada,
        string? sub,
        GrafoConfiguracao grafo)
    {
        ArgumentNullException.ThrowIfNull(tipo);
        ArgumentNullException.ThrowIfNull(grafo);

        if (!tipo.EhResultado)
        {
            return Result<string?>.Success(null);
        }

        if (!confirmada)
        {
            ConfiguracaoDivulgacaoDto politica = ProjecaoDosProdutosPrevistos.PoliticaDeDivulgacaoDaVersao(grafo);
            string campos = string.Join(", ", ConfiguracaoDivulgacao.CamposPermitidos
                .Where(c => politica.CamposPublicos.Contains(c.Codigo, StringComparer.Ordinal))
                .Select(static c => c.Nome.ToLowerInvariant()));

            return Result<string?>.Failure(new DomainError(
                AtoDoProcessoErrorCodes.PoliticaDeDivulgacaoNaoConfirmada,
                $"O tipo de ato '{tipo.Codigo}' é um resultado, e publicá-lo exige confirmar que o PDF segue a política de divulgação do certame, a da versão vigente: campos públicos — {campos}. O sistema não confere o conteúdo do PDF."));
        }

        return string.IsNullOrWhiteSpace(sub)
            ? Result<string?>.Failure(new DomainError(
                AtoDoProcessoErrorCodes.UsuarioNaoIdentificado,
                "O token não traz o claim sub, e a confirmação da política de divulgação precisa registrar quem a fez."))
            : Result<string?>.Success(sub);
    }

    private static string CodigoDoImpedimento(ImpedimentoDoProduto motivo) =>
        motivo switch
        {
            ImpedimentoDoProduto.JaPublicado => AtoDoProcessoErrorCodes.ProdutoJaPublicado,
            ImpedimentoDoProduto.AguardaConfirmacaoDoRegistro => AtoDoProcessoErrorCodes.ProdutoAguardaRegistro,
            ImpedimentoDoProduto.PreliminarNaoPublicado => AtoDoProcessoErrorCodes.PreliminarNaoPublicado,
            ImpedimentoDoProduto.PreliminarAguardaConfirmacao => AtoDoProcessoErrorCodes.PreliminarAguardaRegistro,
            ImpedimentoDoProduto.TipoDeAtoNaoVigente => "ProcessoSeletivo.TipoDeAtoSemVersaoVigente",
            _ => throw new ArgumentOutOfRangeException(nameof(motivo), motivo, "Impedimento do item sem código de erro."),
        };
}
