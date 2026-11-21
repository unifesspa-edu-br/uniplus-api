namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O menor número de integrantes com que uma banca consegue cumprir a deliberação e a correção das
/// etapas que julga: o maior valor entre o quórum e os corretores em campo da regra de consolidação
/// congelada de cada etapa julgada, isto é, os corretores distintos e, quando a regra declara o novo corretor,
/// mais um (o novo corretor é integrante da banca fora dos originais). Não olha o tipo da etapa nem o tipo do processo.
/// </summary>
internal static class MinimoDeIntegrantes
{
    internal static int Exigido(RegraDeDeliberacao regra, IEnumerable<int> corretoresDasJulgadas) =>
        Math.Max(regra.QuorumExigido, corretoresDasJulgadas.DefaultIfEmpty(0).Max());

    internal static DomainError AbaixoDoMinimo(string citacaoDaBanca, string onde, int integrantes, int minimo) =>
        new(
            BancaErrorCodes.IntegrantesAbaixoDoMinimo,
            $"A banca '{citacaoDaBanca}' da {onde} declara {integrantes} integrantes, e o mínimo exigido é {minimo}, o maior entre o quórum e os corretores da regra de consolidação das etapas que ela julga (com o corretor adicional, quando a regra o declara).");

    internal static DomainError NaoResolvida(string citacaoDaBanca, string onde, int posicaoDaEtapa) =>
        new(
            EtapaProcessoErrorCodes.RegraDeConsolidacaoNaoResolvida,
            $"A banca '{citacaoDaBanca}' da {onde} julga a etapa na posição {posicaoDaEtapa + 1}, cuja regra de consolidação não pôde ser confirmada no catálogo; a quantidade de corretores não é conhecida. Tente novamente ou fale com a equipe de suporte.");
}
