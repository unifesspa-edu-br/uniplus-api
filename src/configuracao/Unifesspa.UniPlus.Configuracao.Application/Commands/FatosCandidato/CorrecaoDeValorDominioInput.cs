namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

/// <summary>Corpo da correção de um valor de domínio: o código vai na rota e não muda.</summary>
public sealed record CorrecaoDeValorDominioInput(string? Descricao, string? Orientacao);
