namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Projeta as bancas declaradas e as exigências do lugar em que são realizadas.</summary>
internal static class BancasParaConsequencias
{
    internal static IEnumerable<BancaComExigencias> Ler(ConfiguracaoDeclarada configuracao, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
    [
        .. Posicoes(configuracao, "fases", cronograma.Fases.Count).SelectMany(indice => DaFase(configuracao, cronograma.Fases[indice], indice)),
        .. Posicoes(configuracao, "etapas", etapas.Etapas.Count).SelectMany(indice => DaEtapa(configuracao, etapas.Etapas[indice], indice)),
    ];

    private static IEnumerable<BancaComExigencias> DaFase(ConfiguracaoDeclarada configuracao, FaseDeclarada fase, int indice)
    {
        return fase.Bancas.Declaradas.Select(banca => (banca, posicao: banca.Posicao))
            .OrderBy(par => configuracao.Origem.PosicaoDeOrigem($"fases[{indice}].bancasRequeridas", par.posicao))
            .Select(par => new BancaComExigencias(
                $"fases[{indice}].bancasRequeridas[{par.posicao}].consequenciaDaAusencia", par.banca.ConsequenciaDaAusencia,
                [.. configuracao.Exigencias.DocumentosExigidos.Where(exigencia => exigencia.ExigidoNaFaseId == fase.Id && exigencia.ExigidoNaEtapaId is null
                    && (par.banca.CandidatosAtendidos.Count == 0 || par.banca.CandidatosAtendidos.Any(categoria => categoria == exigencia.TipoDocumentoCategoria)))]));
    }

    private static IEnumerable<BancaComExigencias> DaEtapa(ConfiguracaoDeclarada configuracao, EtapaDeclarada etapa, int indice)
    {
        DocumentoExigido[] exigencias = [.. configuracao.Exigencias.DocumentosExigidos.Where(exigencia => exigencia.ExigidoNaEtapaId == etapa.Id)];
        return etapa.Bancas.Declaradas.Select(banca => (banca, posicao: banca.Posicao))
            .OrderBy(par => configuracao.Origem.PosicaoDeOrigem($"etapas[{indice}].bancas", par.posicao))
            .Select(par => new BancaComExigencias(
                $"etapas[{indice}].bancas[{par.posicao}].consequenciaDaAusencia", par.banca.ConsequenciaDaAusencia, exigencias));
    }

    private static IEnumerable<int> Posicoes(ConfiguracaoDeclarada configuracao, string lista, int quantidade) =>
        Enumerable.Range(0, quantidade).OrderBy(indice => configuracao.Origem.PosicaoDeOrigem(lista, indice));

    internal sealed record BancaComExigencias(string Caminho, ConsequenciaDoDescumprimento? Consequencia, IReadOnlyList<DocumentoExigido> Exigencias);
}
