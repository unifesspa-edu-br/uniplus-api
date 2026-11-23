namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.ValueObjects;

/// <summary>Reúne as chaves que permanecem na edição de cada bloco.</summary>
internal static class ChavesDasBancasNaEdicao
{
    public static ChavesDasBancasDoProcesso DasEtapas(ProcessoSeletivo processo, IReadOnlyList<EtapaProcessoInput> declaradas)
    {
        ChavesDasBancasDoProcesso chaves = new();
        chaves.Reservar(processo.CronogramaFases.SelectMany(static fase => fase.BancasRequeridas).Select(static banca => banca.Chave));
        foreach (EtapaProcesso etapa in processo.Etapas)
        {
            EtapaProcessoInput? declarada = declaradas.FirstOrDefault(item => item.Id == etapa.Id);
            chaves.Reservar(etapa.Bancas.Where(banca => declarada?.Bancas.Any(item => item.Id == banca.Id) == true).Select(static banca => banca.Chave));
        }

        return chaves;
    }

    public static ChavesDasBancasDoProcesso DasFases(ProcessoSeletivo processo, IReadOnlyList<FaseCronogramaInput> declaradas)
    {
        ChavesDasBancasDoProcesso chaves = new();
        chaves.Reservar(processo.Etapas.SelectMany(static etapa => etapa.Bancas).Select(static banca => banca.Chave));
        foreach (FaseCronograma fase in processo.CronogramaFases)
        {
            FaseCronogramaInput? declarada = declaradas.FirstOrDefault(item => item.FaseCanonicaId == fase.FaseCanonicaOrigemId);
            chaves.Reservar(fase.BancasRequeridas.Where(banca => declarada?.BancasRequeridas.Any(item => item.Id == banca.Id) == true).Select(static banca => banca.Chave));
        }

        return chaves;
    }
}
