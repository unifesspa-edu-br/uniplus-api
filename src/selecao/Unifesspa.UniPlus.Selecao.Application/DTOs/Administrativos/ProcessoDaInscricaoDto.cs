namespace Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;

/// <summary>
/// O processo seletivo da inscrição. Não é dado do candidato: é dado do processo, de leitura aberta depois da publicação,
/// e sai para quem tem o acesso à inscrição, fora da projeção por classe.
/// </summary>
/// <param name="Id">A identidade do processo.</param>
/// <param name="Nome">O nome do processo.</param>
public sealed record ProcessoDaInscricaoDto(Guid Id, string Nome);
