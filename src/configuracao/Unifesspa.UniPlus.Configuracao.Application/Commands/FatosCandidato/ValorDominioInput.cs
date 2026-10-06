namespace Unifesspa.UniPlus.Configuracao.Application.Commands.FatosCandidato;

/// <summary>Corpo do acréscimo de um valor de domínio a um fato.</summary>
public sealed record ValorDominioInput(string Codigo, string? Descricao, int Ordem, string? Orientacao = null);
