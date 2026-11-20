namespace Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>O que um membro de enum declarou em <see cref="CodigoDeContratoAttribute"/>, já conferido.</summary>
public sealed record CodigoDeclarado(string Codigo, string Rotulo, string? Descricao);
