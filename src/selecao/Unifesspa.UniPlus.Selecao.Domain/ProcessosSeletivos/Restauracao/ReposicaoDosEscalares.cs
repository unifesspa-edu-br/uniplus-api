namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Restauracao;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os valores únicos que a raiz atribui ao repor a configuração congelada: a raiz é a única que
/// escreve as propriedades, e a política só calcula o que entra em cada uma. A localidade é a
/// única reposição condicional: o grafo que não a traz (anterior à declaração) não apaga a viva.
/// </summary>
internal sealed record ReposicaoDosEscalares(
    OfertaAtendimentoEspecializado OfertaAtendimento,
    ConfiguracaoBonusRegional? BonusRegional,
    int? NumeroDeOpcoesDeCurso,
    ConfiguracaoCascataRemanejamento? Cascata,
    ConfiguracaoClassificacao Classificacao,
    ConfiguracaoDivulgacao? ConfiguracaoDivulgacao,
    ConfiguracaoTaxaInscricao? ConfiguracaoTaxaInscricao,
    LocalidadeRegente? Localidade,
    ReferenciaRegra? AlgoritmoContagemPrazo,
    IdentificadorLegivel? IdentificadorLegivel,
    ReferenciaTemporalFatos? ReferenciaTemporalFatos)
{
    internal static ReposicaoDosEscalares De(GrafoConfiguracao grafo, ReferenciaTemporalFatos? referenciaTemporal)
    {
        ArgumentNullException.ThrowIfNull(grafo);

        return new ReposicaoDosEscalares(
            grafo.OfertaAtendimento,
            grafo.BonusRegional,
            grafo.NumeroDeOpcoesDeCurso,
            grafo.CascataRemanejamento,
            grafo.Classificacao,
            grafo.ConfiguracaoDivulgacao,
            grafo.ConfiguracaoTaxaInscricao,
            grafo.Localidade,
            grafo.AlgoritmoContagemPrazo,
            grafo.IdentificadorLegivel,
            referenciaTemporal);
    }
}
