namespace Unifesspa.UniPlus.Publicacoes.Application.Queries.TiposAtoPublicado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.DTOs;
using Unifesspa.UniPlus.Publicacoes.Application.Mappings;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>
/// O que as consultas de um tipo de ato devolvem: o DTO da versão encontrada, ou o erro de tipo
/// não encontrado quando nenhuma versão viva responde à pergunta.
/// </summary>
internal static class LeituraDoTipoAtoPublicado
{
    internal static Result<TipoAtoPublicadoDto> DtoOuNaoEncontrado(TipoAtoPublicado? tipo) =>
        tipo is null
            ? Result<TipoAtoPublicadoDto>.Failure(new DomainError(TipoAtoPublicadoErrorCodes.NaoEncontrado, "Tipo de ato não encontrado."))
            : Result<TipoAtoPublicadoDto>.Success(tipo.ToDto());
}
