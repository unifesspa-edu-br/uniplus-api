namespace Unifesspa.UniPlus.Publicacoes.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>Mapeamentos HTTP dos erros do tipo de ato publicado (ADR-0023 / ADR-0024).</summary>
/// <remarks>
/// A distinção entre 409 e 422 segue o critério do projeto: <b>422</b> é invariante
/// do próprio payload (formato, tamanho, coerência interna da janela); <b>409</b> é
/// conflito com o estado já gravado. Duas versões vivas do mesmo código valendo no
/// mesmo dia é a segunda coisa — o payload está internamente coerente, o que ele não
/// pode é coexistir com o que já existe.
/// </remarks>
internal static class ErrosDoTipoDeAto
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Mapeamentos =
    [
        new(TipoAtoPublicadoErrorCodes.VigenciaSobreposta,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.publicacoes.tipo_ato.vigencia_sobreposta",
                "Já existe uma versão viva deste tipo de ato vigente em parte do período informado")),

        new(TipoAtoPublicadoErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.publicacoes.tipo_ato.nao_encontrado",
                "Tipo de ato não encontrado")),

        new(TipoAtoPublicadoErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.tipo_ato.codigo_obrigatorio",
                "Código do tipo de ato é obrigatório")),

        new(TipoAtoPublicadoErrorCodes.CodigoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.tipo_ato.codigo_tamanho",
                "Tamanho do código do tipo de ato inválido")),

        new(TipoAtoPublicadoErrorCodes.CodigoFormato,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.tipo_ato.codigo_formato",
                "Formato do código do tipo de ato inválido")),

        new(TipoAtoPublicadoErrorCodes.CodigoImutavel,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.tipo_ato.codigo_imutavel",
                "O código do tipo de ato é a sua identidade e não pode ser alterado")),

        new(TipoAtoPublicadoErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.tipo_ato.nome_obrigatorio",
                "Nome do tipo de ato é obrigatório")),

        new(TipoAtoPublicadoErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.tipo_ato.nome_tamanho",
                "Tamanho do nome do tipo de ato inválido")),

        new(TipoAtoPublicadoErrorCodes.BaseLegalTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.tipo_ato.base_legal_tamanho",
                "Tamanho da base legal inválido")),

        new(TipoAtoPublicadoErrorCodes.VigenciaFimAnteriorAoInicio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.tipo_ato.vigencia_fim_anterior_ao_inicio",
                "Fim da vigência deve ser posterior ao início")),

        new(TipoAtoPublicadoErrorCodes.ResultadoComEfeitoIrreversivel,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.tipo_ato.resultado_com_efeito_irreversivel",
                "Tipo de resultado não pode ter efeito irreversível")),
    ];
}
