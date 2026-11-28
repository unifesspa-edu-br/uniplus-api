namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;

using Unifesspa.UniPlus.Configuracao.Application.Commands.FasesCanonicas;

/// <summary>
/// Comandos completos e válidos de criação e atualização de tipo de fase, com os cinco sinalizadores
/// informados como falsos. Cada teste muda só o que exercita, com <c>with</c>.
/// </summary>
internal static class ApoioDosComandosDeFaseCanonica
{
    internal static CriarFaseCanonicaCommand Criacao(string? codigo) =>
        new(
            codigo,
            Nome: "Fase de teste",
            OrigemData: "NO_EDITAL",
            AgrupaEtapas: false,
            PermiteComplementacao: false,
            ColetaInscricao: false,
            ColetaSolicitacaoIsencao: false,
            OrganizadaEmChamadas: false);

    internal static AtualizarFaseCanonicaCommand Atualizacao(Guid id) =>
        new(
            id,
            Nome: "Fase de teste",
            OrigemData: "NO_EDITAL",
            AgrupaEtapas: false,
            PermiteComplementacao: false,
            ColetaInscricao: false,
            ColetaSolicitacaoIsencao: false,
            OrganizadaEmChamadas: false);
}
