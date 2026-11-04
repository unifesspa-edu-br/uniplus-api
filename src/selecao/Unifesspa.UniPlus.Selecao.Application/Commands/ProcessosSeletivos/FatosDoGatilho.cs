namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Services;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O catálogo de fatos do candidato na forma que a conferência dos gatilhos das exigências usa: o
/// vocabulário fechado, estendido pelos fatos categóricos de escopo-processo, o universo de fatos que o
/// processo resolve, o domínio dinâmico da oferta do próprio processo e a fase em que cada fato é conhecido.
/// </summary>
internal sealed record FatosDoGatilho(
    IReadOnlyDictionary<string, DescritorFatoCandidato> Vocabulario,
    IReadOnlyDictionary<string, string> PontoResolucaoPorFato,
    IReadOnlyDictionary<string, string> MembroPorAgregado,
    IReadOnlySet<string> Resolviveis,
    IReadOnlyDictionary<string, DominioDeValores> DominiosDinamicos,
    IReadOnlyDictionary<string, FatoCandidatoView> Catalogo)
{
    /// <summary>Lê o catálogo de fatos, a única consulta cross-módulo da conferência dos gatilhos.</summary>
    public static async Task<FatosDoGatilho> LerAsync(
        ProcessoSeletivo processo, IFatoCandidatoReader fatoCandidatoReader, CancellationToken cancellationToken)
    {
        IReadOnlyList<FatoCandidatoView> catalogo = await fatoCandidatoReader
            .ListarAsync(cancellationToken)
            .ConfigureAwait(false);
        return new FatosDoGatilho(
            VocabularioFechado(catalogo),
            VocabularioDeFatos.PontoResolucaoPorFato(catalogo),
            VocabularioDeFatos.MembroPorAgregado(catalogo),
            VocabularioDeFatos.QueOProcessoResolve(processo, catalogo),
            VocabularioDeFatos.DominiosDinamicos(processo, catalogo),
            catalogo.ToDictionary(static f => f.Codigo, StringComparer.Ordinal));
    }

    private static Dictionary<string, DescritorFatoCandidato> VocabularioFechado(IReadOnlyList<FatoCandidatoView> catalogo)
    {
        Dictionary<string, DescritorFatoCandidato> vocabulario = [];
        foreach (FatoCandidatoView fato in catalogo)
        {
            if (VocabularioDeFatos.Classificar(fato) is not { } tipo)
            {
                continue;
            }

            Result<DescritorFatoCandidato> descritor = DescritorFatoCandidato.Criar(fato.Codigo, tipo, fato.ValoresDominio);
            if (descritor.IsSuccess)
            {
                vocabulario[fato.Codigo] = descritor.Value!;
            }
        }

        return vocabulario;
    }
}
