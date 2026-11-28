namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Services;

using Kernel.Results;

/// <summary>
/// Resolve as derivações do pedido contra o vocabulário de fatos do candidato e a oferta do processo, acumulando as
/// recusas (ADR-0125) de todas as derivações: primeiro as de forma, depois as de semântica e por fim as de coerência com o domínio do fato, cada grupo na ordem
/// do pedido, com o campo precedido da posição da derivação. A derivação recusada não esconde a recusa de outra.
/// </summary>
internal static class ResolucaoDasDerivacoes
{
    public static Result<List<ConfiguracaoDerivacaoFato>> Resolver(
        ProcessoSeletivo processo,
        IReadOnlyList<ConfiguracaoDerivacaoInput> entradas,
        ContextoDoCatalogo contexto)
    {
        IReadOnlySet<string> universo = FatosDisponiveis(processo, entradas);
        List<ResolucaoDaDerivacao> resolucoes =
            [.. entradas.Select(entrada => DerivacaoInformada.De(entrada).Resolver(contexto, universo))];
        List<FieldError> recusas = [.. RecusasPorGrupo(resolucoes, static (erro, i) => erro.NoCaminhoDe($"configuracoes[{i}]"))];

        return recusas.Count > 0
            ? Result<List<ConfiguracaoDerivacaoFato>>.ValidationFailure(recusas)
            : Result<List<ConfiguracaoDerivacaoFato>>.Success([.. resolucoes.Select(static r => r.Configuracao!)]);
    }

    // Os fatos disponíveis na configuração final: os coletados pelo processo, os derivados definidos neste mesmo
    // pedido (a substituição é integral) e os que a oferta supre quando o processo não configura nenhum — a modalidade,
    // a opção pelas cotas e o egresso pela regra do sistema. Uma condição só pode citar um fato deste universo,
    // coerente com a recusa de fato citado inexistente da publicação.
    private static HashSet<string> FatosDisponiveis(ProcessoSeletivo processo, IReadOnlyList<ConfiguracaoDerivacaoInput> entradas)
    {
        HashSet<string> universo = new(
            DerivacoesDaOferta.Efetivas([], processo.DistribuicaoVagas).Select(static d => d.CodigoFato), StringComparer.Ordinal);
        universo.UnionWith(processo.FatosColetados.Select(static f => f.FatoCodigo));
        universo.UnionWith(entradas.Select(static e => e.CodigoFato).OfType<string>());

        return universo;
    }

    /// <summary>
    /// As recusas de forma, depois as de semântica e depois as de coerência, cada grupo na ordem das derivações;
    /// <paramref name="naPosicao"/> diz como cada recusa se localiza na derivação de índice dado.
    /// </summary>
    public static IEnumerable<FieldError> RecusasPorGrupo(
        IReadOnlyList<ResolucaoDaDerivacao> resolucoes, Func<FieldError, int, FieldError> naPosicao) =>
        NaPosicao(resolucoes, static r => r.RecusasDeForma, naPosicao)
            .Concat(NaPosicao(resolucoes, static r => r.RecusasDeSemantica, naPosicao))
            .Concat(NaPosicao(resolucoes, static r => r.RecusasDeCoerencia, naPosicao));

    private static IEnumerable<FieldError> NaPosicao(
        IReadOnlyList<ResolucaoDaDerivacao> resolucoes,
        Func<ResolucaoDaDerivacao, IReadOnlyList<FieldError>> recusasDoGrupo,
        Func<FieldError, int, FieldError> naPosicao) =>
        resolucoes.SelectMany((resolucao, i) => recusasDoGrupo(resolucao).Select(erro => naPosicao(erro, i)));
}
