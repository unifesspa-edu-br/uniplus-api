namespace Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Como o fato novo se identifica e se apresenta: o código, o nome, a descrição e o componente de exibição
/// (ADR-0136). Entra no cadastro junto com a <see cref="DefinicaoDoFato"/>, que traz os eixos de valor, o escopo e a
/// proteção de dados.
/// </summary>
/// <param name="Codigo">Código único do fato, em maiúsculas.</param>
/// <param name="Nome">Rótulo legível do fato.</param>
/// <param name="Descricao">O que o dado significa, para quem analisa a inscrição.</param>
/// <param name="ComponenteExibicao">Como a tela exibe o valor quando o modo não se deduz do tipo; ausente quando o fato não declara.</param>
public sealed record DescritivoDoFato(
    string Codigo,
    string Nome,
    string? Descricao,
    CodigoInformado<ComponenteDeExibicao> ComponenteExibicao = default);
