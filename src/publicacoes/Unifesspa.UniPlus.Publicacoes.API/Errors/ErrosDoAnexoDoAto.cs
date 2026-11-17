namespace Unifesspa.UniPlus.Publicacoes.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>Mapeamentos HTTP dos erros dos anexos do ato.</summary>
internal static class ErrosDoAnexoDoAto
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Mapeamentos =
    [
        // Anexos: 422, é o próprio payload que não permite identificar ou provar o anexo.
        new(AnexoDoAtoErrorCodes.HashInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.anexo_do_ato.hash_invalido",
                "O hash do anexo deve ser um SHA-256 em hexadecimal minúsculo")),

        new(AnexoDoAtoErrorCodes.NomeInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.anexo_do_ato.nome_invalido",
                "O nome do anexo é obrigatório e tem tamanho máximo")),

        new(AnexoDoAtoErrorCodes.NomeDuplicado,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.anexo_do_ato.nome_duplicado",
                "Há mais de um anexo com o mesmo nome no ato")),

        new(AnexoDoAtoErrorCodes.QuantidadeExcedida,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.anexo_do_ato.quantidade_excedida",
                "O ato excede o número máximo de anexos")),

        new(AnexoDoAtoErrorCodes.Nulo,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.anexo_do_ato.nulo",
                "A lista de anexos não admite elemento nulo")),

        new(AnexoDoAtoErrorCodes.TipoMidiaInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.anexo_do_ato.tipo_midia_invalido",
                "O tipo de mídia do anexo deve ter a forma tipo/subtipo")),

    ];
}
