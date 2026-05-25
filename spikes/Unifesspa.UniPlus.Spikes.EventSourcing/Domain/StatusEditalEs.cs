namespace Unifesspa.UniPlus.Spikes.EventSourcing.Domain;

/// <summary>Ciclo de vida do edital event-sourced usado como cobaia do spike.</summary>
public enum StatusEditalEs
{
    /// <summary>Valor não inicializado (default de struct).</summary>
    Nenhum = 0,

    /// <summary>Stream recém-aberto, ainda não publicado.</summary>
    Rascunho = 1,

    /// <summary>Edital publicado; pode ser retificado por novos fatos.</summary>
    Publicado = 2,
}
