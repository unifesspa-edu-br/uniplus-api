namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// Compara o que a versão nova vai congelar de um fato já coletado com o que as versões publicadas
/// congelaram (UNI-REQ-0144): o metadado dos fatos citados por condição de documento exigido e a
/// operação de cada agregado, que o handler resolve do catálogo na hora de congelar. Os itens são
/// conferidos no agregado, ao serem regravados, e de novo no fechamento da sessão. A comparação é sempre contra o que a versão nova
/// congelaria, nunca contra o catálogo vivo: edição do catálogo que a versão nova não congela não
/// recusa a retificação.
/// </summary>
internal static class ConferenciaDosEixosDosFatosColetados
{
    /// <summary>O que a versão reidratada congelou, no formato que o rascunho guarda.</summary>
    public static EixosDeVersaoPublicada DaVersao(EnvelopeReidratado versao)
    {
        ArgumentNullException.ThrowIfNull(versao);
        return Congelado(versao.MetadadosFatosCongelados, versao.AgregadosDosGrupos);
    }

    /// <summary>
    /// Lê as versões publicadas do processo, uma vez por retificação sem sessão. Sem versão, devolve
    /// nulo. A versão que o sistema não sabe reidratar recusa, como a abertura da sessão.
    /// </summary>
    public static async Task<Result<FatosDasVersoesPublicadas?>> LerAsync(
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        Guid processoSeletivoId,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<VersaoConfiguracao> versoes = await processoSeletivoRepository
            .ObterVersoesAsync(processoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (versoes.Count == 0)
        {
            return Result<FatosDasVersoesPublicadas?>.Success(null);
        }

        Result<VersoesPublicadasLidas> lidas = VersoesPublicadasLidas.Ler(registroCodecs, versoes, abrindoSessao: false);
        return lidas.IsFailure
            ? Result<FatosDasVersoesPublicadas?>.Failure(lidas.Error!)
            : Result<FatosDasVersoesPublicadas?>.Success(lidas.Value!.Fatos);
    }

    public static DomainError? Conferir(
        FatosDasVersoesPublicadas publicadas,
        IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadados,
        IReadOnlyList<DefinicaoAgregado> agregados)
    {
        ArgumentNullException.ThrowIfNull(publicadas);
        ArgumentNullException.ThrowIfNull(agregados);

        EixosDeVersaoPublicada novo = Congelado(metadados, agregados);
        return publicadas.PrimeiroEixoCongeladoTrocado(novo.Metadados, novo.OperacaoDosAgregados)?.ParaErro();
    }

    private static EixosDeVersaoPublicada Congelado(
        IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadados, IReadOnlyList<DefinicaoAgregado> agregados) =>
        new(
            (metadados ?? new Dictionary<string, MetadadoFatoCongelado>()).ToDictionary(
                static par => par.Key,
                static par => new EixosDoMetadadoCongelado(par.Value.Dominio, par.Value.Cardinalidade, par.Value.PontoResolucao),
                StringComparer.Ordinal),
            agregados.ToDictionary(static a => a.Codigo, static a => AgregadoDeGrupo.ParaToken(a.Operacao), StringComparer.Ordinal));
}
