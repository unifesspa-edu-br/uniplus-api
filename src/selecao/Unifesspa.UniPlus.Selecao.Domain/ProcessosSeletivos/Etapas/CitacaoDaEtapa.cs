namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;

/// <summary>Como a configuração do processo cita a nota de uma etapa; citar a nota é um dos sinais de que a etapa tem nota.</summary>
public sealed record CitacaoDaEtapa(bool CitadaPeloDesempate, IReadOnlyList<decimal> NotasMinimasDaEliminacao)
{
    public bool CitaANota => CitadaPeloDesempate || NotasMinimasDaEliminacao.Count > 0;
}
