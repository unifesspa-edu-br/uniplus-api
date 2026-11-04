namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.ProcessosSeletivos.Publicacao;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// O que a publicação, a retificação e o fechamento da sessão fazem antes de canonicalizar: o catálogo é
/// editável depois que o item copiou dele a origem dos valores, o formato e a proteção, a unidade
/// responsável das fases pode ter mudado no cadastro e o tipo de documento pode ter sido reclassificado, e
/// os gates avaliam a cópia. Tudo é conferido antes e a recusa sai num só lote (ADR-0125); só então o
/// agregado refaz as cópias.
/// </summary>
internal static class PreparoDoCongelamento
{
    /// <param name="fatosJaColetados">Os fatos que alguma versão publicada já coletou; nulo na publicação inicial.</param>
    /// <param name="daVersaoVigente">O que a versão vigente congelou das exigências; nulo na publicação inicial.</param>
    /// <returns>A proteção do processo que a canonicalização congela, ou as recusas de itens e unidades somadas.</returns>
    internal static async Task<Result<ProtecaoVigenteDoProcesso>> PrepararAsync(
        ProcessoSeletivo processo,
        CatalogoConferido catalogo,
        IReadOnlySet<string>? fatosJaColetados,
        CongeladoNaVersaoVigente? daVersaoVigente,
        IResolvedorDaUnidadeResponsavel unidadeResponsavelResolvedor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(catalogo);

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
        Result preparado = processo.PrepararCongelamento(
            new PreparacaoDoCongelamento(copias.Value!.Copias, copias.Value.FatosJaColetados, unidades.Value!, protecao.Exigencias));

        return preparado.IsSuccess
            ? Result<ProtecaoVigenteDoProcesso>.Success(protecao)
            : Result<ProtecaoVigenteDoProcesso>.ValidationFailure(preparado.Errors);
    }
}
