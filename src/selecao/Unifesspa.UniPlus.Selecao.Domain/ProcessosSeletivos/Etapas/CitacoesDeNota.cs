namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

/// <summary>As etapas que a configuração do processo cita por nota: o desempate pela maior nota e a eliminação por nota mínima.</summary>
/// <param name="EtapasDoDesempate">As etapas cuja maior nota decide um critério de desempate.</param>
/// <param name="NotasMinimasDaEliminacao">As notas mínimas que as regras de eliminação por nota mínima exigem de cada etapa.</param>
public sealed record CitacoesDeNota(
    IReadOnlyCollection<Guid> EtapasDoDesempate,
    IReadOnlyList<(Guid EtapaId, decimal NotaMinima)> NotasMinimasDaEliminacao)
{
    public static CitacoesDeNota Nenhuma { get; } = new([], []);

    public CitacaoDaEtapa De(Guid etapaId) => new(
        EtapasDoDesempate.Contains(etapaId),
        [.. NotasMinimasDaEliminacao.Where(citacao => citacao.EtapaId == etapaId).Select(static citacao => citacao.NotaMinima)]);
}
