namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Entities;
using Domain.Services;
using Domain.ValueObjects;

/// <summary>
/// A configuração que a avaliação da inscrição lê, montada da mesma definição avaliável do formulário
/// que se publica: a viva do rascunho, para a pré-visualização, e a congelada da versão, para o
/// candidato (UNI-REQ-0084).
/// </summary>
/// <remarks>
/// Cada oferta de curso tem a distribuição de vagas dela, com as modalidades e as regras dela. A
/// configuração congelada da inscrição vale para as ofertas que o candidato escolheu — a 1ª e a 2ª
/// opção, quando a versão admite duas: as modalidades ofertadas e as derivações saem só da união das
/// distribuições dessas ofertas. Sem oferta escolhida, valem todas as distribuições — o candidato
/// ainda não escolheu, a tela mostra o que pode vir a valer e o envio exige a oferta. A configuração
/// viva, da pré-visualização, não tem candidato nem oferta e também vale pela união.
/// </remarks>
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
            [.. processo.DistribuicaoVagas.SelectMany(static d => d.Modalidades)],
            processo.CronogramaFases,
            processo.Formularios);
    }

    /// <param name="grafo">O grafo congelado da versão.</param>
    /// <param name="avaliavel">A definição avaliável, montada para as mesmas <paramref name="ofertasDeCurso"/>.</param>
    /// <param name="ofertasDeCurso">As ofertas de curso escolhidas; vazia, ou ausente, quando ainda não há escolha.</param>
    public static ConfiguracaoAvaliavel DoCongelado(
        GrafoConfiguracao grafo, DefinicaoAvaliavel avaliavel, IReadOnlyCollection<Guid>? ofertasDeCurso = null)
    {
        ArgumentNullException.ThrowIfNull(grafo);
        ArgumentNullException.ThrowIfNull(avaliavel);

        return new ConfiguracaoAvaliavel(
            avaliavel.Definicao,
            avaliavel.Ofertas,
            ArvoreExigenciasCongelada.DeGrafoReidratado([.. grafo.NosExigencia.Where(static n => n.NoPaiId is null)]),
            grafo.FatosColetados,
            grafo.GruposColetados,
            [.. DerivacoesDaOferta.DistribuicaoDasOfertas(grafo.DistribuicaoVagas, ofertasDeCurso ?? []).SelectMany(static d => d.Modalidades)],
            grafo.CronogramaFases,
            grafo.Formularios);
    }
}
