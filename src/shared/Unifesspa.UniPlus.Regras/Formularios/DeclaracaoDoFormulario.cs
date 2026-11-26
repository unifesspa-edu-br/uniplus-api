namespace Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O que o formulário declara de si: se serve à fase que coleta a inscrição e se é obrigatório
/// (UNI-REQ-0144, ADR-0171).
/// </summary>
public readonly record struct DeclaracaoDoFormulario(bool ServeAFaseDeInscricao, bool Obrigatorio)
{
    /// <summary>A declaração do formulário que o candidato responde para se inscrever.</summary>
    public static DeclaracaoDoFormulario DaInscricao { get; } = new(ServeAFaseDeInscricao: true, Obrigatorio: true);
}
