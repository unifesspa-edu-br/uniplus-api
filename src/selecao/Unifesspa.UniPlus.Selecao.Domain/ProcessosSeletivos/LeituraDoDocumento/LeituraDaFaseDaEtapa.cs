namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Lê a fase que a etapa cita pela chave e devolve o código da fase que a etapa guarda. A fase que a leitura adotou dá o código; a
/// que a leitura recusou deixa a etapa sem fase e sem causa nova, porque a causa é a da fase; a chave que nenhuma fase do documento
/// tem é recusa da etapa. A regra que recusou a fase não se repete aqui.
/// </summary>
internal static class LeituraDaFaseDaEtapa
{
    private const string CampoDaFase = "faseCodigo";
    private const string CodigoDaRecusa = "ProcessoSeletivo.EtapaSemFaseNoCronograma";

    /// <summary>O código da fase que a etapa cita, nulo quando ela não cita fase ou a fase foi recusada, e a recusa da chave que nenhuma fase tem.</summary>
    internal static ValorLido<string?> Ler(EtapaNoDocumento etapa, FasesLidas fases)
    {
        ArgumentNullException.ThrowIfNull(etapa);
        ArgumentNullException.ThrowIfNull(fases);

        return etapa.Identificacao.FaseChave switch
        {
            null => new(null, []),
            { } chave when fases.CodigoPelaChave.TryGetValue(chave, out string? codigo) => new(codigo, []),
            { } chave when fases.ChavesRecusadas.Contains(chave) => new(null, []),
            _ => new(null, [Recusa(etapa)]),
        };
    }

    private static RecusaDaParte Recusa(EtapaNoDocumento etapa) => new(
        CampoDaFase,
        CampoDaFase,
        etapa.PosicaoNoDocumento,
        etapa.Chave,
        new DomainError(CodigoDaRecusa, "A etapa declara uma fase que não está no cronograma deste processo."));
}
