namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>O valor que a divergência da governança comparou: o anterior e o novo, em texto estável.</summary>
public sealed record ValorDivergente(string De, string Para);
