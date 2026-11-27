namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

/// <summary>
/// O que as etapas e a classificação citam: os tipos de etapa, os tipos de banca e os atos que as etapas publicam, as regras do
/// catálogo e a resolução dos pesos por área do ENEM. Cada lista sai sem repetição, na ordem em que aparece.
/// </summary>
internal sealed record CodigosDaAvaliacaoCitados(
    IReadOnlyList<string> TiposDeEtapa,
    IReadOnlyList<string> TiposDeBanca,
    IReadOnlyList<string> TiposDeAto,
    IReadOnlyList<(string Codigo, string Versao)> Regras,
    string? ResolucaoDePesos);

/// <summary>
/// Junta, nas etapas e na classificação, os códigos que o documento cita em cada cadastro: o tipo de etapa, o tipo de banca e o ato
/// publicado pela etapa, a regra de apuração, as de recurso e de procedimento, as de cálculo, arredondamento, ordem de alocação,
/// eliminação e desempate, e a resolução dos pesos. Não confere o que cada código é.
/// </summary>
internal static class CodigosCitadosNaAvaliacao
{
    internal static CodigosDaAvaliacaoCitados Em(DeclaracoesDoDocumento declaracoes)
    {
        ArgumentNullException.ThrowIfNull(declaracoes);

        EtapaNoDocumento[] etapas = [.. declaracoes.Etapas];

        return new(
            CodigosDistintos.De(etapas.Select(static etapa => etapa.Identificacao.TipoEtapaCodigo)),
            CodigosDistintos.De(etapas.SelectMany(static etapa => etapa.Bancas).Select(static banca => banca.TipoBancaCodigo)),
            CodigosDistintos.De(etapas.SelectMany(static etapa => etapa.PublicacoesPrevistas).Select(static produto => produto.AtoCodigo)),
            CodigosDistintos.DasRegras([.. RegrasDasEtapas(etapas), .. RegrasDaClassificacao(declaracoes.Classificacao)]),
            declaracoes.Classificacao?.ResolucaoPesoAreaEnem);
    }

    private static IEnumerable<ReferenciaDeRegraNoDocumento?> RegrasDasEtapas(EtapaNoDocumento[] etapas)
    {
        RegraDeRecursoNoDocumento[] regrasDeRecurso = [.. etapas.SelectMany(static etapa => etapa.Recursos).Select(static recurso => recurso.Regra)];

        return
        [
            .. etapas.Select(static etapa => etapa.Avaliacao.RegraDeApuracao),
            .. regrasDeRecurso.Select(static regra => regra.Regra),
            .. regrasDeRecurso.SelectMany(static regra => regra.Procedimentos).Select(static procedimento => procedimento.Regra),
        ];
    }

    private static IEnumerable<ReferenciaDeRegraNoDocumento?> RegrasDaClassificacao(ClassificacaoNoDocumento? classificacao) => classificacao is null
        ? []
        :
        [
            classificacao.RegraCalculo,
            classificacao.RegraArredondamento,
            classificacao.RegraOrdemAlocacao,
            .. classificacao.RegrasEliminacao.Select(static regra => regra.Regra),
            .. classificacao.CriteriosDeDesempate.Select(static criterio => criterio.Regra),
        ];
}
