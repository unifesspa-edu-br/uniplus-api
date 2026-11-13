namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>Quando a votação da banca se encerra.</summary>
public enum EncerramentoDaVotacao
{
    /// <summary>Só depois do voto de todos os titulares.</summary>
    TodosOsTitulares = 1,

    /// <summary>A presidência encerra ao atingir o quórum.</summary>
    PresidenciaNoQuorum = 2,
}
