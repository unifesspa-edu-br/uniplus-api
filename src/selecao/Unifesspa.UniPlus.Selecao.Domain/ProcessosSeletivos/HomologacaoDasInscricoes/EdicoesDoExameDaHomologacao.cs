namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using System.Globalization;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;

/// <summary>
/// A edição do exame de cada condição de confirmação do INEP precisa ser uma das edições do ENEM que o processo declara
/// (UNI-REQ-0439): a condição de uma edição que nenhum candidato pode informar na inscrição nunca seria cumprida. A edição é
/// comparada pelo ano, com quatro algarismos, com o código da opção declarada. Sem edições declaradas não há o que comparar,
/// e a condição segue conferida só pelo intervalo de anos. A regra que a leitura do documento recusou não é julgada.
/// </summary>
internal static class EdicoesDoExameDaHomologacao
{
    private const string CampoDaEdicao = "edicaoDoExame";

    internal static IReadOnlyList<FieldError> Causas(CronogramaDoProcesso cronograma, IReadOnlyList<OpcaoDeclaradaFato> opcoesDeclaradas)
    {
        ArgumentNullException.ThrowIfNull(cronograma);
        ArgumentNullException.ThrowIfNull(opcoesDeclaradas);

        HashSet<string> declaradas = EdicoesDeclaradas(opcoesDeclaradas);

        return declaradas.Count == 0
            ? []
            : [.. cronograma.Fases
                .Select(static (fase, posicao) => (Fase: fase, Posicao: posicao))
                .Where(static par => ExigenciaDaHomologacaoPorOrigem.HomologaAsInscricoes(par.Fase))
                .SelectMany(par => CausasDaRegra(par.Fase.Atos.RegraDeHomologacao!, par.Posicao, declaradas))];
    }

    private static HashSet<string> EdicoesDeclaradas(IReadOnlyList<OpcaoDeclaradaFato> opcoesDeclaradas) =>
        [.. opcoesDeclaradas
            .Where(static opcao => string.Equals(opcao.FatoCodigo, EdicaoDoEnem.Fato, StringComparison.Ordinal))
            .Select(static opcao => opcao.Codigo)];

    private static IEnumerable<FieldError> CausasDaRegra(RegraDeHomologacaoDasInscricoes regra, int posicaoDaFase, HashSet<string> declaradas) =>
        regra.Alternativas.SelectMany((alternativa, posicaoDaAlternativa) => alternativa.Condicoes
            .Select(static (condicao, posicaoDaCondicao) => (Condicao: condicao, Posicao: posicaoDaCondicao))
            .Where(par => ConfirmaOInepDeEdicaoNaoDeclarada(par.Condicao, declaradas))
            .Select(par => CronogramaDoProcesso.NaFase(
                posicaoDaFase,
                $"{FaseDeclarada.CampoDaRegraDeHomologacao}.alternativas[{posicaoDaAlternativa}].condicoes[{par.Posicao}].{CampoDaEdicao}",
                EdicaoNaoDeclarada())));

    private static bool ConfirmaOInepDeEdicaoNaoDeclarada(CondicaoDeHomologacao condicao, HashSet<string> declaradas) =>
        condicao.Tipo == TipoDeCondicaoDeHomologacao.ResultadoNoRetornoDoInep
        && !declaradas.Contains(condicao.EdicaoDoExame.ToString(CultureInfo.InvariantCulture));

    private static DomainError EdicaoNaoDeclarada() => new(
        HomologacaoDasInscricoesErrorCodes.EdicaoDoExameNaoDeclarada,
        "A edição do exame da condição não está entre as edições do ENEM que o processo declara.");
}
