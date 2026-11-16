namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O menor número de integrantes com que uma banca consegue cumprir a deliberação e a correção das
/// etapas que julga: o maior valor entre o quórum e os corretores em campo da regra de consolidação
/// congelada de cada etapa julgada, isto é, os corretores distintos e, quando a regra declara o novo corretor,
/// mais um (o novo corretor é integrante da banca fora dos originais). Não olha o tipo da etapa nem o tipo do processo.
/// </summary>
internal static class MinimoDeIntegrantes
{
    internal static int Exigido(RegraDeDeliberacao regra, IEnumerable<EtapaProcesso> julgadas, CorretoresDasEtapas corretores) =>
        Math.Max(regra.QuorumExigido, julgadas.Select(corretores.De).DefaultIfEmpty(0).Max());

    /// <summary>
    /// Primeiro as bancas comprovadamente abaixo do mínimo, de todas as bancas, e depois as etapas cuja regra de
    /// consolidação o catálogo não confirmou: o mínimo se confere sobre as etapas cuja regra foi lida, e a recusa por
    /// regra não resolvida não esconde uma banca que já se sabe insuficiente.
    /// </summary>
    internal static IEnumerable<DomainError> Violacoes(EstadoDaConfiguracao estado, CorretoresDasEtapas corretores)
    {
        List<BancaJulgadora> bancas = [.. BancasDasFases(estado), .. BancasDasEtapas(estado)];

        return
        [
            .. bancas.Select(banca => AbaixoDoMinimo(banca, corretores)).OfType<DomainError>(),
            .. bancas.SelectMany(banca => banca.Julgadas.Where(etapa => !corretores.Resolvida(etapa)).Select(etapa => NaoResolvida(banca, etapa))),
        ];
    }

    private static IEnumerable<BancaJulgadora> BancasDasFases(EstadoDaConfiguracao estado) =>
        estado.CronogramaFases.SelectMany(fase => fase.BancasRequeridas
            .Where(static banca => banca.RegraDeDeliberacao is not null)
            .Select(banca => new BancaJulgadora(
                banca.Nome,
                $"da fase '{fase.Codigo}'",
                banca.RegraDeDeliberacao!,
                [.. JulgamentoDasBancas.EtapasJulgadasPelaBancaDaFase(estado, fase, banca)])));

    private static IEnumerable<BancaJulgadora> BancasDasEtapas(EstadoDaConfiguracao estado) =>
        estado.Etapas.SelectMany(etapa => etapa.Bancas
            .Where(static banca => banca.RegraDeDeliberacao is not null)
            .Select(banca => new BancaJulgadora(
                banca.Nome,
                $"da etapa '{Eco(etapa.Nome)}'",
                banca.RegraDeDeliberacao!,
                [.. JulgamentoDasBancas.EtapasJulgadasPelaBancaDaEtapa(etapa, banca)])));

    private static DomainError? AbaixoDoMinimo(BancaJulgadora banca, CorretoresDasEtapas corretores)
    {
        int minimo = Exigido(banca.Regra, banca.Julgadas.Where(corretores.Resolvida), corretores);

        return banca.Regra.Integrantes >= minimo
            ? null
            : new DomainError(
                BancaErrorCodes.IntegrantesAbaixoDoMinimo,
                $"A banca '{Eco(banca.Nome)}' {banca.Onde} declara {banca.Regra.Integrantes} integrantes, e o mínimo exigido é {minimo}, o maior entre o quórum e os corretores da regra de consolidação das etapas que ela julga (com o corretor adicional, quando a regra declara novo corretor).");
    }

    private static DomainError NaoResolvida(BancaJulgadora banca, EtapaProcesso etapa) =>
        new(
            BancaErrorCodes.RegraDeConsolidacaoNaoResolvida,
            $"A banca '{Eco(banca.Nome)}' {banca.Onde} julga a etapa '{Eco(etapa.Nome)}', cuja regra de consolidação não pôde ser confirmada no catálogo, e a quantidade de corretores não é conhecida. Tente novamente ou fale com a equipe de suporte.");

    private static string Eco(string texto) =>
        CaracteresInvisiveis.ParaEco(texto, TextoCongelado.TamanhoMaximoEcoado);

    /// <summary>Uma banca declarada com regra de deliberação, onde ela está e as etapas que julga.</summary>
    private sealed record BancaJulgadora(string Nome, string Onde, RegraDeDeliberacao Regra, IReadOnlyList<EtapaProcesso> Julgadas);
}
