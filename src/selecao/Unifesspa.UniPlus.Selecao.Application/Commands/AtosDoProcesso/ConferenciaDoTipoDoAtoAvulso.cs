namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.Entities;
using Domain.Errors;

using ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Confere no catálogo de Publicações, antes de publicar o ato, que o tipo declarado serve a um ato
/// avulso: vigora no dia da publicação, não congela configuração e o catálogo o declara publicável
/// como documento avulso.
/// </summary>
/// <remarks>
/// Quais tipos são avulsos — aviso, comunicado, errata —
/// é cadastro: resultado, convocação e chamada (esta do Ingresso) ficam de fora porque o catálogo não os
/// declara publicáveis, e nenhum código compara o tipo com um literal (ADR-0103). O tipo que congela é
/// recusado à parte porque o ato diria uma coisa e nenhuma versão de configuração a provaria.
/// <para>
/// O documento avulso não é item do edital: o tipo dele não é declarado no cronograma, e a versão publicada
/// só congela a classificação dos tipos que o cronograma declara. Por isso as classificações que importam
/// aqui — se congela e se é único — vêm do catálogo vigente no dia da publicação, e não da versão publicada.
/// </para>
/// </remarks>
public sealed class ConferenciaDoTipoDoAtoAvulso(ConferenciaDoTipoDeAto tiposDeAto)
{
    /// <summary>A forma do tipo no pedido, conferida sem consultar o catálogo: o tipo ausente ou em branco.</summary>
    public static IReadOnlyList<FieldError> RecusaDaForma(string? tipoAtoCodigo) =>
        string.IsNullOrWhiteSpace(tipoAtoCodigo) ? [AusenciasNosDadosDoAto.Tipo()] : [];

    internal async Task<Result<TipoAtoPublicadoView>> ConferirAsync(
        string tipoAtoCodigo,
        DateOnly diaDaPublicacao,
        CancellationToken cancellationToken)
    {
        Result<TipoAtoPublicadoView> vigente = await tiposDeAto
            .VigenteAsync(tipoAtoCodigo, diaDaPublicacao, cancellationToken)
            .ConfigureAwait(false);
        Result<TipoAtoPublicadoView> conferido = vigente.IsFailure ? vigente : Classificar(vigente.Value!);

        return conferido.IsFailure
            ? Result<TipoAtoPublicadoView>.ValidationFailure([new FieldError(AusenciasNosDadosDoAto.CampoDoTipo, conferido.Error!)])
            : conferido;
    }

    private static Result<TipoAtoPublicadoView> Classificar(TipoAtoPublicadoView tipo)
    {
        if (tipo.CongelaConfiguracao)
        {
            return Result<TipoAtoPublicadoView>.Failure(new DomainError(
                AtoDoProcessoErrorCodes.TipoCongelaConfiguracao,
                "O tipo de ato congela configuração e só se publica com a publicação ou a retificação do edital — o documento avulso não altera a configuração do processo."));
        }

        return tipo.PublicavelComoAvulso
            ? Result<TipoAtoPublicadoView>.Success(tipo)
            : Result<TipoAtoPublicadoView>.Failure(new DomainError(
                AtoDoProcessoErrorCodes.TipoNaoPublicavelComoAvulso,
                "O tipo de ato não é publicável como documento avulso: resultado, convocação e chamada têm fluxo próprio, e o documento avulso é o aviso, o comunicado e a errata."));
    }

    /// <summary>
    /// O tipo que corrige sempre outro ato exige o ato corrigido; só se pergunta a um pedido que não indica ato nenhum,
    /// e o motivo vem junto dele.
    /// </summary>
    public static Result ExigeAtoCorrigido(TipoAtoPublicadoView tipo)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        return tipo.ExigeAtoCorrigido
            ? Result.ValidationFailure([new FieldError(AtoDoProcesso.CampoDoAtoCorrigido, new DomainError(
                AtoDoProcessoErrorCodes.TipoExigeAtoCorrigido,
                "O tipo de ato corrige sempre outro ato publicado: informe o ato corrigido e o motivo da correção."))])
            : Result.Success();
    }
}
