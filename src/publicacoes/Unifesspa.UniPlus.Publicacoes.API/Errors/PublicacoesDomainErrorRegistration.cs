namespace Unifesspa.UniPlus.Publicacoes.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>
/// Mapeia os erros de domínio do módulo Publicações para <c>ProblemDetails</c>
/// (ADR-0023 / ADR-0024).
/// </summary>
/// <remarks>
/// A distinção entre 409 e 422 segue o critério do projeto: <b>422</b> é invariante
/// do próprio payload (formato, tamanho, coerência interna da janela); <b>409</b> é
/// conflito com o estado já gravado. Duas versões vivas do mesmo código valendo no
/// mesmo dia é a segunda coisa — o payload está internamente coerente, o que ele não
/// pode é coexistir com o que já existe.
/// </remarks>
[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, PublicacoesDomainErrorRegistration>().")]
internal sealed class PublicacoesDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        new(TipoAtoPublicadoErrorCodes.VigenciaSobreposta,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.publicacoes.tipo_ato.vigencia_sobreposta",
                "Já existe uma versão viva deste tipo de ato vigente em parte do período informado")),

        new(TipoAtoPublicadoErrorCodes.IdDivergente,
            new DomainErrorMapping(
                StatusCodes.Status400BadRequest,
                "uniplus.publicacoes.tipo_ato.id_divergente",
                "O identificador da URL não corresponde ao do corpo da requisição")),

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

        new(AtoNormativoErrorCodes.TipoSemVersaoVigente,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.ato_normativo.tipo_sem_versao_vigente",
                "Não há versão vigente do tipo de ato na data de publicação")),

        new(AtoNormativoErrorCodes.VersaoInvocadaIncompleta,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.ato_normativo.versao_invocada_incompleta",
                "A versão invocada deve trazer o par (id, hash) completo ou nenhum dos dois")),

        new(AtoNormativoErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.publicacoes.ato_normativo.nao_encontrado",
                "Ato normativo não encontrado")),

        new(AtoNormativoErrorCodes.AtoRetificadoNaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.ato_normativo.ato_retificado_nao_encontrado",
                "O ato retificado não corresponde a nenhum ato registrado")),

        new(AtoNormativoErrorCodes.ClasseDeCongelamentoDivergente,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.publicacoes.ato_normativo.classe_congelamento_divergente",
                "A classe de congelamento do ato que retifica deve coincidir com a do ato retificado")),

        // 409 e não 422: é conflito com o estado já gravado — o ato-alvo já tem um
        // retificador —, o mesmo critério que classifica a vigência sobreposta como 409.
        new(AtoNormativoErrorCodes.RaizJaRetificada,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.publicacoes.ato_normativo.raiz_ja_retificada",
                "O ato que se tentou retificar já foi retificado por outro (a cadeia é linear)")),
    ];
}
