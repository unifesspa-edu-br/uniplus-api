namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Quando a votação da banca se encerra.</summary>
public enum EncerramentoDaVotacao
{
    /// <summary>Só depois do voto de todos os titulares.</summary>
    [CodigoDeContrato("TODOS_OS_TITULARES", "Todos os titulares votam")]
    TodosOsTitulares = 1,

    /// <summary>A presidência encerra ao atingir o quórum.</summary>
    [CodigoDeContrato("PRESIDENCIA_NO_QUORUM", "A presidência encerra ao atingir o quórum")]
    PresidenciaNoQuorum = 2,
}
