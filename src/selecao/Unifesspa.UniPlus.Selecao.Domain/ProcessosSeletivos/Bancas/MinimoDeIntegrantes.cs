namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O menor número de integrantes com que uma banca consegue cumprir a deliberação e a correção das
/// etapas que julga: o maior valor entre o quórum e os corretores da regra de consolidação
/// congelada de cada etapa julgada. Não olha o tipo da etapa nem o tipo do processo.
/// </summary>
internal static class MinimoDeIntegrantes
{
    internal static int Exigido(RegraDeDeliberacao regra, IEnumerable<EtapaProcesso> julgadas, CorretoresDasEtapas corretores) =>
        Math.Max(regra.QuorumExigido, julgadas.Select(corretores.De).DefaultIfEmpty(0).Max());

    internal static IEnumerable<DomainError> Violacoes(EstadoDaConfiguracao estado, CorretoresDasEtapas corretores) =>
    [
        .. estado.CronogramaFases.SelectMany(fase => ViolacoesDaFase(estado, fase, corretores)),
        .. estado.Etapas.SelectMany(etapa => ViolacoesDaEtapa(etapa, corretores)),
    ];

    private static IEnumerable<DomainError> ViolacoesDaFase(
        EstadoDaConfiguracao estado, FaseCronograma fase, CorretoresDasEtapas corretores) =>
        fase.BancasRequeridas
            .Where(static banca => banca.RegraDeDeliberacao is not null)
            .Select(banca => Conferir(
                banca.Nome,
                $"da fase '{fase.Codigo}'",
                banca.RegraDeDeliberacao!,
                JulgamentoDasBancas.EtapasJulgadasPelaBancaDaFase(estado, fase, banca),
                corretores))
            .OfType<DomainError>();

    private static IEnumerable<DomainError> ViolacoesDaEtapa(EtapaProcesso etapa, CorretoresDasEtapas corretores) =>
        etapa.Bancas
            .Where(static banca => banca.RegraDeDeliberacao is not null)
            .Select(banca => Conferir(banca.Nome, $"da etapa '{Eco(etapa.Nome)}'", banca.RegraDeDeliberacao!, JulgamentoDasBancas.EtapasJulgadasPelaBancaDaEtapa(etapa, banca), corretores))
            .OfType<DomainError>();

    private static DomainError? Conferir(
        string nome, string onde, RegraDeDeliberacao regra, IEnumerable<EtapaProcesso> julgadas, CorretoresDasEtapas corretores)
    {
        int minimo = Exigido(regra, julgadas, corretores);
        return regra.Integrantes >= minimo
            ? null
            : new DomainError(
                BancaErrorCodes.IntegrantesAbaixoDoMinimo,
                $"A banca '{Eco(nome)}' {onde} declara {regra.Integrantes} integrantes, e o mínimo exigido é {minimo}, o maior entre o quórum e os corretores da regra de consolidação das etapas que ela julga.");
    }

    private static string Eco(string texto) =>
        CaracteresInvisiveis.ParaEco(texto, TextoCongelado.TamanhoMaximoEcoado);
}
