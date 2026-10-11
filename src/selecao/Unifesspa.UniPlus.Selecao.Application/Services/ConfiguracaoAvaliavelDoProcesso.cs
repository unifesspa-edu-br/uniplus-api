namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Entities;
using Domain.ValueObjects;

/// <summary>
/// A configuração que a avaliação da inscrição lê, montada da mesma definição avaliável do formulário
/// que se publica: a viva do rascunho, para a pré-visualização, e a congelada da versão, para o
/// candidato (UNI-REQ-0084).
/// </summary>
public static class ConfiguracaoAvaliavelDoProcesso
{
    public static ConfiguracaoAvaliavel DaConfiguracaoViva(ProcessoSeletivo processo, DefinicaoAvaliavel avaliavel)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(avaliavel);

        return new ConfiguracaoAvaliavel(
            avaliavel.Definicao,
            avaliavel.Ofertas,
            ArvoreExigenciasCongelada.DaConfiguracaoViva(processo),
            processo.FatosColetados,
            processo.GruposColetados,
            [.. processo.DistribuicaoVagas.SelectMany(static d => d.Modalidades)]);
    }

    public static ConfiguracaoAvaliavel DoCongelado(GrafoConfiguracao grafo, DefinicaoAvaliavel avaliavel)
    {
        ArgumentNullException.ThrowIfNull(grafo);
        ArgumentNullException.ThrowIfNull(avaliavel);

        return new ConfiguracaoAvaliavel(
            avaliavel.Definicao,
            avaliavel.Ofertas,
            ArvoreExigenciasCongelada.DeGrafoReidratado([.. grafo.NosExigencia.Where(static n => n.NoPaiId is null)]),
            grafo.FatosColetados,
            grafo.GruposColetados,
            [.. grafo.DistribuicaoVagas.SelectMany(static d => d.Modalidades)]);
    }
}
