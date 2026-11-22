namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>Estado da repetição própria ou herdada durante a conferência.</summary>
internal sealed record RepeticaoDaRegra
{
    private enum Estado { SemRepeticao, GrupoAceito, Recusada }

    private RepeticaoDaRegra(Estado estado, string? grupo)
    {
        Situacao = estado;
        Grupo = grupo;
    }

    private Estado Situacao { get; }
    internal string? Grupo { get; }
    internal bool Recusada => Situacao == Estado.Recusada;
    internal static RepeticaoDaRegra SemRepeticao { get; } = new(Estado.SemRepeticao, null);

    internal RepeticaoDaRegra Avancar(string? grupo, bool recusada)
    {
        if (Recusada || recusada)
        {
            return new(Estado.Recusada, null);
        }
        string? aceito = Grupo ?? grupo;
        return aceito is null ? SemRepeticao : new(Estado.GrupoAceito, aceito);
    }
}
