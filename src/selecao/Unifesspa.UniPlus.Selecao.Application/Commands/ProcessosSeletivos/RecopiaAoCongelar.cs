namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.ProcessosSeletivos.Publicacao;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>A proteção do processo que a canonicalização congela e os fatos que as versões publicadas já coletaram.</summary>
internal sealed record RecopiadoAoCongelar(ProtecaoVigenteDoProcesso Protecao, FatosDasVersoesPublicadas? Publicadas);

/// <summary>
/// O que a publicação, a retificação e o fechamento da sessão fazem antes de canonicalizar: o catálogo é
/// editável depois que o item copiou dele a origem dos valores, o formato e a proteção, a unidade
/// responsável das fases pode ter mudado no cadastro e o tipo de documento pode ter sido reclassificado, e
/// os gates avaliam a cópia. Tudo é conferido antes e a recusa sai num só lote (ADR-0125); só então o
/// agregado refaz as cópias.
/// </summary>
public sealed class RecopiaAoCongelar(IResolvedorDaUnidadeResponsavel unidadeResponsavelResolvedor, LeituraDasVersoesPublicadas versoesPublicadas)
{
    /// <returns>
    /// A proteção do processo que a canonicalização congela e os fatos já coletados que a origem informa; ou a
    /// recusa da leitura das versões publicadas, que precede as recusas de itens e unidades, somadas num lote.
    /// </returns>
    internal async Task<Result<RecopiadoAoCongelar>> RecopiarAsync(
        ProcessoSeletivo processo, CatalogoVivo catalogo, OrigemDoCongelamento origem, CancellationToken cancellationToken)
    {
        Result<FatosDasVersoesPublicadas?> publicadas = await origem
            .FatosJaPublicadosAsync(versoesPublicadas, processo, cancellationToken)
            .ConfigureAwait(false);
        if (publicadas.IsFailure)
        {
            return Result<RecopiadoAoCongelar>.Failure(publicadas.Error!);
        }

        Result<ProtecaoVigenteDoProcesso> recopia = await RecopiarCopiasEUnidadesAsync(
            processo, catalogo, publicadas.Value?.FatosJaColetados, origem.CongeladoNaVersaoVigente, cancellationToken)
            .ConfigureAwait(false);
        return recopia.IsFailure
            ? Result<RecopiadoAoCongelar>.ValidationFailure(recopia.Errors)
            : Result<RecopiadoAoCongelar>.Success(new RecopiadoAoCongelar(recopia.Value!, publicadas.Value));
    }

    private async Task<Result<ProtecaoVigenteDoProcesso>> RecopiarCopiasEUnidadesAsync(
        ProcessoSeletivo processo,
        CatalogoVivo catalogo,
        IReadOnlySet<string>? fatosJaColetados,
        CongeladoNaVersaoVigente? daVersaoVigente,
        CancellationToken cancellationToken)
    {
        Result<CopiasConferidas> copias = ReconferenciaDasCopiasDoItem.Conferir(processo, catalogo.Fatos, fatosJaColetados);
        Result<IReadOnlyDictionary<Guid, UnidadeResponsavelSnapshot>> unidades = await ReconferenciaDasUnidadesDasFases
            .ConferirAsync(processo, unidadeResponsavelResolvedor, cancellationToken)
            .ConfigureAwait(false);
        if (copias.IsFailure || unidades.IsFailure)
        {
            return Result<ProtecaoVigenteDoProcesso>.ValidationFailure([.. copias.Errors, .. unidades.Errors]);
        }

        // O tipo de documento é editável depois que a exigência copiou dele a proteção: o valor vigente é
        // o que se congela, e a montagem não lê o que o agregado refaz a seguir.
        ProtecaoVigenteDoProcesso protecao = ProtecaoVigenteDasExigenciasDoProcesso.Montar(processo, catalogo.TiposVivos, catalogo.Fatos, daVersaoVigente);

        return Recopiar(processo, copias.Value!, unidades.Value!, protecao);
    }

    private static Result<ProtecaoVigenteDoProcesso> Recopiar(
        ProcessoSeletivo processo,
        CopiasConferidas copias,
        IReadOnlyDictionary<Guid, UnidadeResponsavelSnapshot> unidades,
        ProtecaoVigenteDoProcesso protecao)
    {
        Result recopiado = processo.RecopiarAoCongelar(new CopiasDoCongelamento(copias.Copias, copias.FatosJaColetados, unidades, protecao.Exigencias));

        return recopiado.IsSuccess
            ? Result<ProtecaoVigenteDoProcesso>.Success(protecao)
            : Result<ProtecaoVigenteDoProcesso>.ValidationFailure(recopiado.Errors);
    }
}
