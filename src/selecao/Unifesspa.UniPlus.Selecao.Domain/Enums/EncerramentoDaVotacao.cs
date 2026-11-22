namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>Quando a votação da banca se encerra.</summary>
public enum EncerramentoDaVotacao
{
    /// <summary>Só depois do voto de todos os titulares.</summary>
    [CodigoDeContrato("todosOsTitulares", "Todos os titulares votam")]
    TodosOsTitulares = 1,

    /// <summary>A presidência encerra ao atingir o quórum.</summary>
    [CodigoDeContrato("presidenciaNoQuorum", "A presidência encerra ao atingir o quórum")]
    PresidenciaNoQuorum = 2,
}
