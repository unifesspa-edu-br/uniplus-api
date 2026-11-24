namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

/// <summary>
/// A declaração de um tipo de documento depois de conferida: código como value object, textos sem espaço nas pontas,
/// categoria normalizada, campos opcionais ausentes quando vazios e a proteção de dados completa.
/// </summary>
public sealed record CamposDoTipoDocumento(
    CodigoTipoDocumento Codigo,
    string Nome,
    string? Descricao,
    string Categoria,
    string? FormatosAceitos,
    int? TamanhoMaximoMb,
    string? TipoEquivalente,
    ProtecaoDeDados Protecao);
