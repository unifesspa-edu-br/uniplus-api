namespace Unifesspa.UniPlus.Authorization.Contracts;

/// <summary>
/// Marca a propriedade de texto que sempre sai com o token canônico de uma classificação de proteção
/// (<c>PUBLICO</c>, <c>INTERNO</c>, <c>PESSOAL</c>, <c>IDENTIFICADOR</c> ou <c>SENSIVEL</c>). O contrato OpenAPI a publica
/// obrigatória, não anulável e com o vocabulário fechado.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class TokenDeClassificacaoAttribute : Attribute;
