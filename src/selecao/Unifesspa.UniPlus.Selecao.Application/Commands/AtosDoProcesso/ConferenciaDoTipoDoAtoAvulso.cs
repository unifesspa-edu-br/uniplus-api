namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.Errors;

using ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Confere no catálogo de Publicações, antes de enviar o ato, que o tipo declarado serve a um ato
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
internal static class ConferenciaDoTipoDoAtoAvulso
{
    public static async Task<Result<TipoAtoPublicadoView>> ConferirAsync(
        ITipoAtoPublicadoReader reader,
        string tipoAtoCodigo,
        DateOnly diaDaPublicacao,
        CancellationToken cancellationToken)
    {
        Result<TipoAtoPublicadoView> vigente = await ConferenciaDoTipoDeAto
            .VigenteAsync(reader, tipoAtoCodigo, diaDaPublicacao, cancellationToken)
            .ConfigureAwait(false);
        if (vigente.IsFailure)
        {
            return vigente;
        }

        TipoAtoPublicadoView tipo = vigente.Value!;

        if (tipo.CongelaConfiguracao)
        {
            return Result<TipoAtoPublicadoView>.Failure(new DomainError(
                AtoDoProcessoErrorCodes.TipoCongelaConfiguracao,
                $"O tipo de ato '{tipo.Codigo}' congela configuração e só se publica com a publicação ou a retificação do edital — o documento avulso não altera a configuração do processo."));
        }

        return tipo.PublicavelComoAvulso
            ? Result<TipoAtoPublicadoView>.Success(tipo)
            : Result<TipoAtoPublicadoView>.Failure(new DomainError(
                AtoDoProcessoErrorCodes.TipoNaoPublicavelComoAvulso,
                $"O tipo de ato '{tipo.Codigo}' não é publicável como documento avulso: resultado, convocação e chamada têm fluxo próprio, e o documento avulso é o aviso, o comunicado e a errata."));
    }

    /// <summary>
    /// O tipo que corrige sempre outro ato exige o ato corrigido; o motivo vem junto dele (validação do
    /// comando).
    /// </summary>
    public static Result ExigeAtoCorrigido(TipoAtoPublicadoView tipo, Guid? atoCorrigidoId)
    {
        ArgumentNullException.ThrowIfNull(tipo);

        return tipo.ExigeAtoCorrigido && atoCorrigidoId is null
            ? Result.Failure(new DomainError(
                AtoDoProcessoErrorCodes.TipoExigeAtoCorrigido,
                $"O tipo de ato '{tipo.Codigo}' corrige sempre outro ato publicado: informe o ato corrigido e o motivo da correção."))
            : Result.Success();
    }
}
