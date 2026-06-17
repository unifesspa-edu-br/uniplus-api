namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using System.Diagnostics.CodeAnalysis;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>
/// Registry de mapeamentos de erros de domínio do Configuracao para wire codes
/// / status HTTP. Cobre os cadastros <c>Campus</c> e <c>LocalOferta</c> e a
/// validação da referência de cidade do Geo (UNI-REQ #587 · ADR-0090).
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instanciada via IServiceProvider.AddSingleton<IDomainErrorRegistration, ConfiguracaoDomainErrorRegistration>().")]
internal sealed class ConfiguracaoDomainErrorRegistration : IDomainErrorRegistration
{
    public IEnumerable<KeyValuePair<string, DomainErrorMapping>> GetMappings() =>
    [
        // ── Referência de cidade do Geo (compartilhada por Campus e LocalOferta) ──
        new(CidadeReferenciaErrorCodes.CodigoIbgeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.cidade_referencia.codigo_ibge_obrigatorio",
                "Código IBGE da cidade é obrigatório")),

        new(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.cidade_referencia.codigo_ibge_formato_invalido",
                "Código IBGE da cidade em formato inválido")),

        new(CidadeReferenciaErrorCodes.UfObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.cidade_referencia.uf_obrigatoria",
                "UF da cidade é obrigatória")),

        new(CidadeReferenciaErrorCodes.UfIncoerente,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.cidade_referencia.uf_incoerente",
                "UF informada incompatível com o prefixo do código IBGE")),

        new(CidadeReferenciaErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.cidade_referencia.nome_obrigatorio",
                "Nome da cidade é obrigatório")),

        // ── Campus ────────────────────────────────────────────────────────
        new(CampusErrorCodes.SiglaObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.campus.sigla_obrigatoria",
                "Sigla do campus é obrigatória")),

        new(CampusErrorCodes.SiglaTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.campus.sigla_tamanho",
                "Tamanho da sigla do campus inválido")),

        new(CampusErrorCodes.SiglaJaExiste,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.campus.sigla_ja_existe",
                "Já existe um campus ativo com esta sigla")),

        new(CampusErrorCodes.NomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.campus.nome_obrigatorio",
                "Nome do campus é obrigatório")),

        new(CampusErrorCodes.NomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.campus.nome_tamanho",
                "Tamanho do nome do campus inválido")),

        new(CampusErrorCodes.EnderecoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.campus.endereco_tamanho",
                "Tamanho do endereço do campus inválido")),

        new(CampusErrorCodes.CepInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.campus.cep_invalido",
                "CEP do campus em formato inválido")),

        new(CampusErrorCodes.LatitudeForaDeFaixa,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.campus.latitude_fora_de_faixa",
                "Latitude do campus fora da faixa válida")),

        new(CampusErrorCodes.LongitudeForaDeFaixa,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.campus.longitude_fora_de_faixa",
                "Longitude do campus fora da faixa válida")),

        new(CampusErrorCodes.CodigoEmecTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.campus.codigo_emec_tamanho",
                "Tamanho do código e-MEC do campus inválido")),

        new(CampusErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.campus.nao_encontrado",
                "Campus não encontrado")),

        new(CampusErrorCodes.RemocaoBloqueadaPorLocalOferta,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.campus.remocao_bloqueada_por_local_oferta",
                "Não é possível remover um campus responsável por locais de oferta ativos")),

        // ── LocalOferta ───────────────────────────────────────────────────
        new(LocalOfertaErrorCodes.TipoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.local_oferta.tipo_invalido",
                "Tipo de local de oferta inválido")),

        new(LocalOfertaErrorCodes.CampusResponsavelNaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.local_oferta.campus_responsavel_nao_encontrado",
                "Campus responsável informado não encontrado")),

        new(LocalOfertaErrorCodes.EnderecoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.local_oferta.endereco_tamanho",
                "Tamanho do endereço do local de oferta inválido")),

        new(LocalOfertaErrorCodes.CodigoEmecTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.local_oferta.codigo_emec_tamanho",
                "Tamanho do código e-MEC do local de oferta inválido")),

        new(LocalOfertaErrorCodes.NaoEncontrado,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.local_oferta.nao_encontrado",
                "Local de oferta não encontrado")),

        new(LocalOfertaErrorCodes.RemocaoBloqueadaPorOfertaCurso,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.local_oferta.remocao_bloqueada_por_oferta_curso",
                "Não é possível remover um local de oferta referenciado por oferta de curso ativa")),
    ];
}
