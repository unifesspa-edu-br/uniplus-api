namespace Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Formulário de inscrição pronto para renderização (Story #559): título, termo de aceite e os
/// fatos coletados na ordem de coleta, cada um com rótulo, tipo de renderização, obrigatoriedade
/// e a pré-condição já congelada. Projetado da <c>VersaoConfiguracao</c> vigente — nunca da raiz
/// viva — pelo <c>FormularioInscricaoController</c>, endpoint público.
/// </summary>
public sealed record FormularioRenderizavelDto(
    string? Titulo,
    string? TermoAceiteTexto,
    IReadOnlyList<FatoColetadoDto> FatosColetados);
