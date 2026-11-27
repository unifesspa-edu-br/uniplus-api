namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A distribuição de vagas de uma oferta do documento, lida pela fábrica da entidade: os cadastros citados primeiro, depois a
/// declaração (a forma e a conferência) e, por fim, o quadro. A causa fica no campo do documento e na posição enviada; o que o
/// cadastro recusou não gera a causa da regra que o lê.
/// </summary>
internal static class LeituraDaDistribuicaoDeVagas
{
    internal static DistribuicaoLida Ler(DistribuicaoNoDocumento distribuicao, CadastrosCitadosNoDocumento cadastros)
    {
        ArgumentNullException.ThrowIfNull(distribuicao);
        ArgumentNullException.ThrowIfNull(cadastros);

        CadastrosDaDistribuicaoLidos resolvidos = LeituraDosCadastrosDaDistribuicao.Ler(distribuicao, cadastros);
        (ConfiguracaoDistribuicaoVagas? lida, IReadOnlyList<FieldError> causas) = Montar(distribuicao, resolvidos);
        int[] posicoes = [.. distribuicao.Modalidades.Select(static modalidade => modalidade.PosicaoNoDocumento)];

        return new(lida, distribuicao.PosicaoNoDocumento, posicoes, Recusadas(distribuicao.PosicaoNoDocumento, causas, posicoes));
    }

    private static IReadOnlyList<ParteRecusada> Recusadas(int posicao, IReadOnlyList<FieldError> causas, int[] posicoesDasModalidades) =>
        RecusasDasVagas.Partes(
            LeituraDasDistribuicoesDeVagas.Lista,
            posicao,
            $"{LeituraDasDistribuicoesDeVagas.Lista}[{posicao}]",
            causas,
            OrigemDasLinhasDoQuadro(posicao, posicoesDasModalidades));

    // O índice da linha do quadro que a regra aponta é o da modalidade na lista enviada, e a posição enviada dela é a do ponteiro.
    private static OrigemNoDocumento OrigemDasLinhasDoQuadro(int posicaoDaDistribuicao, int[] posicoesDasModalidades) => new(
        new Dictionary<string, IReadOnlyList<int>>(StringComparer.Ordinal)
        {
            [$"{LeituraDasDistribuicoesDeVagas.Lista}[{posicaoDaDistribuicao}].quadro"] = posicoesDasModalidades,
        },
        []);

    private static (ConfiguracaoDistribuicaoVagas? Lida, IReadOnlyList<FieldError> Causas) Montar(
        DistribuicaoNoDocumento distribuicao, CadastrosDaDistribuicaoLidos resolvidos)
    {
        if (resolvidos.Recusas.Count > 0)
        {
            return (null, [.. resolvidos.Recusas, .. ConferirSemMontar(distribuicao, resolvidos)]);
        }

        Result<ConfiguracaoDistribuicaoVagas> criada = ConfiguracaoDistribuicaoVagas.Criar(
            resolvidos.OfertaOrigemId!.Value, resolvidos.Declarada, resolvidos.Fatos);

        return criada.IsSuccess ? (criada.Value, []) : (null, criada.Errors);
    }

    // O dado que o cadastro recusou fica fora da declaração e consta como recusado, e a conferência só deixa de rodar a regra que o lê.
    private static IReadOnlyList<FieldError> ConferirSemMontar(DistribuicaoNoDocumento distribuicao, CadastrosDaDistribuicaoLidos resolvidos) =>
    [
        .. ConferenciaDaDistribuicaoDeVagas.ConferirForma(
            resolvidos.Declarada.VagasOfertadasBase,
            resolvidos.Declarada.PercentualDaReservaLegal,
            distribuicao.Modalidades.Count,
            [.. distribuicao.Modalidades.Select(static modalidade => modalidade.QuantidadeDeclarada)]),
        .. ConfiguracaoDistribuicaoVagas.ConferirDeclaracao(resolvidos.Declarada, resolvidos.Fatos),
    ];
}
