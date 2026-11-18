namespace Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

using Kernel.Results;

/// <summary>Uma causa de recusa e a precedência dela quando outra causa aponta o mesmo ponto: menor vence.</summary>
internal readonly record struct CausaClassificada(int Precedencia, FieldError Causa);
