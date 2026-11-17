namespace Unifesspa.UniPlus.Publicacoes.Domain.Errors;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;

/// <summary>Erros dos anexos de um ato que o domínio e os handlers produzem com a mesma mensagem.</summary>
public static class AnexoDoAtoErros
{
    public static DomainError QuantidadeExcedida(int quantidadeEnviada) => new(
        AnexoDoAtoErrorCodes.QuantidadeExcedida,
        $"O ato admite no máximo {AnexoDoAto.QuantidadeMaxima} anexos; foram enviados {quantidadeEnviada}.");

    public static DomainError NomeInvalido() => new(
        AnexoDoAtoErrorCodes.NomeInvalido,
        $"O nome do anexo é obrigatório e deve ter no máximo {AnexoDoAto.NomeMaxLength} caracteres.");

    public static DomainError TipoMidiaInvalido(string nome) => new(
        AnexoDoAtoErrorCodes.TipoMidiaInvalido,
        $"O tipo de mídia do anexo '{nome}' deve ter a forma tipo/subtipo (ex.: application/pdf).");

    public static DomainError HashInvalido(string nome) => new(
        AnexoDoAtoErrorCodes.HashInvalido,
        $"O hash do anexo '{nome}' deve ser um SHA-256 em hexadecimal minúsculo (64 caracteres).");

    public static DomainError NomeDuplicado(string nome) => new(
        AnexoDoAtoErrorCodes.NomeDuplicado,
        $"Há mais de um anexo chamado '{nome}' no mesmo ato.");

    public static DomainError Nulo() => new(
        AnexoDoAtoErrorCodes.Nulo,
        "A lista de anexos não admite elemento nulo.");
}
