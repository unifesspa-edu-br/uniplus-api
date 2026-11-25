namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Application.Queries.ObrigatoriedadesLegais;

/// <summary>
/// Recusas da obrigatoriedade legal (ADR-0058): campo, forma do predicado, referências a cadastros e unicidade.
/// Os códigos de campo saem do domínio (ADR-0125), um por violação, com o campo em <c>errors[]</c>.
/// </summary>
internal static class ErrosDaObrigatoriedadeLegal
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        .. DoCodigoEDoTexto(),
        .. DoTipoDeProcessoDaCategoriaEDaVigencia(),
        .. DaFormaDoPredicado(),
        .. DasReferenciasAosCadastros(),
        .. DasReferenciasDeAtendimentoEDesempate(),
        .. DaUnicidadeEDoEstado(),
    ];

    // Código da regra e texto normativo: presença e tamanho.
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DoCodigoEDoTexto() =>
    [
        new("ObrigatoriedadeLegal.RegraCodigoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.regra_codigo_obrigatorio", "RegraCodigo obrigatório")),
        new("ObrigatoriedadeLegal.RegraCodigoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.regra_codigo_invalido", "RegraCodigo inválido")),
        new("ObrigatoriedadeLegal.BaseLegalObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.base_legal_obrigatoria", "BaseLegal obrigatória")),
        new("ObrigatoriedadeLegal.BaseLegalInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.base_legal_invalida", "BaseLegal inválida")),
        new("ObrigatoriedadeLegal.DescricaoHumanaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.descricao_humana_obrigatoria", "DescricaoHumana obrigatória")),
        new("ObrigatoriedadeLegal.DescricaoHumanaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.descricao_humana_invalida", "DescricaoHumana inválida")),
        new("ObrigatoriedadeLegal.AtoNormativoUrlInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.ato_normativo_url_invalido", "AtoNormativoUrl inválido")),
        new("ObrigatoriedadeLegal.PortariaInternaCodigoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.portaria_interna_codigo_invalido", "PortariaInternaCodigo inválido")),
    ];

    // Tipo de processo, categoria e vigência.
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DoTipoDeProcessoDaCategoriaEDaVigencia() =>
    [
        new("ObrigatoriedadeLegal.TipoProcessoCodigoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.tipo_processo_codigo_obrigatorio", "TipoProcessoCodigo obrigatório")),
        new("ObrigatoriedadeLegal.TipoProcessoCodigoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.tipo_processo_codigo_invalido", "Código do tipo de processo fora do formato")),
        new("ObrigatoriedadeLegal.CategoriaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.categoria_invalida", "Categoria inválida")),
        new("ObrigatoriedadeLegal.VigenciaInicioObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.vigencia_inicio_obrigatorio", "VigenciaInicio obrigatório")),
        new("ObrigatoriedadeLegal.VigenciaInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.vigencia_invalida", "Vigência inválida")),
    ];

    // Forma do predicado, conferida sem consulta.
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaFormaDoPredicado() =>
    [
        new("ObrigatoriedadeLegal.PredicadoObrigatorio", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.predicado_obrigatorio", "Predicado obrigatório")),
        new("ObrigatoriedadeLegal.ModalidadesMinimasVazia", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.modalidades_minimas_vazia", "Exigência de modalidades mínimas sem nenhuma modalidade")),
        new("ObrigatoriedadeLegal.PredicadoComCodigoEmBranco", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.predicado_com_codigo_em_branco", "Predicado da regra legal com código em branco")),
        new("ObrigatoriedadeLegal.AtendimentoDisponivelVazio",
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.selecao.obrigatoriedade_legal.atendimento_disponivel_vazio",
                "Exigência de atendimento disponível sem nenhum tipo de deficiência")),
    ];

    // Referências do predicado e do tipo de processo a cadastros vivos.
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DasReferenciasAosCadastros() =>
    [
        new("ObrigatoriedadeLegal.TipoProcessoNaoEncontradoOuInativo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.tipo_processo_nao_encontrado_ou_inativo", "Tipo de processo seletivo não encontrado ou não está ativo")),
        new("ObrigatoriedadeLegal.TipoEtapaNaoEncontradoOuInativo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.tipo_etapa_nao_encontrado_ou_inativo", "Tipo de etapa não encontrado ou não está ativo")),
        new("ObrigatoriedadeLegal.ModalidadeNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.modalidade_nao_encontrada", "Modalidade não encontrada entre as vivas do cadastro")),
        new("ObrigatoriedadeLegal.TipoDocumentoNaoEncontrado", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.obrigatoriedade_legal.tipo_documento_nao_encontrado", "Tipo de documento não encontrado entre os vivos do cadastro")),
    ];

    // Referências do predicado a tipos de deficiência e a critérios de desempate do catálogo.
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DasReferenciasDeAtendimentoEDesempate() =>
    [
        new("ObrigatoriedadeLegal.TipoDeficienciaNaoEncontrada",
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.selecao.obrigatoriedade_legal.tipo_deficiencia_nao_encontrada",
                "Tipo de deficiência não encontrado entre os vivos do cadastro")),
        new("ObrigatoriedadeLegal.CriterioDesempateNaoEncontrado",
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.selecao.obrigatoriedade_legal.criterio_desempate_nao_encontrado",
                "Critério de desempate não encontrado no catálogo de regras")),
    ];

    // Unicidade da regra ativa e regra inexistente.
    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> DaUnicidadeEDoEstado() =>
    [
        new("ObrigatoriedadeLegal.RegraCodigoDuplicada", new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.selecao.obrigatoriedade_legal.regra_codigo_duplicada", "Já existe regra ativa com este RegraCodigo")),
        new("ObrigatoriedadeLegal.HashColisao", new DomainErrorMapping(StatusCodes.Status409Conflict, "uniplus.selecao.obrigatoriedade_legal.hash_colisao", "Colisão de hash de regra ativa")),
        new("ObrigatoriedadeLegal.NaoEncontrada", new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.obrigatoriedade_legal.nao_encontrada", "ObrigatoriedadeLegal não encontrada")),
        new(FiltroDeCategoriaDasObrigatoriedadesLegais.CodigoDeErro, new DomainErrorMapping(StatusCodes.Status400BadRequest, "uniplus.selecao.obrigatoriedade_legal.filtro_de_categoria_invalido", "Categoria do filtro de obrigatoriedades legais inválida")),
    ];
}
