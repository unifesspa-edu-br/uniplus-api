namespace Unifesspa.UniPlus.Configuracao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;

/// <summary>Mapeamentos HTTP dos erros de Oferta de curso (UNI-REQ-0010).</summary>
internal static class OfertaCursoErrorMappings
{
    internal static readonly IReadOnlyList<KeyValuePair<string, DomainErrorMapping>> Todos =
    [
        new(OfertaCursoErrorCodes.CursoInexistenteOuRemovido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.curso_inexistente_ou_removido",
                "Curso informado não existe ou foi removido")),

        new(OfertaCursoErrorCodes.LocalOfertaInexistenteOuRemovido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.local_oferta_inexistente_ou_removido",
                "Local de oferta informado não existe ou foi removido")),

        new(OfertaCursoErrorCodes.UnidadeOfertanteInexistente,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.unidade_ofertante_inexistente",
                "Unidade ofertante informada não existe ou foi removida — não há identidade viva para congelar")),

        new(OfertaCursoErrorCodes.UnidadeOfertanteOrigemObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.unidade_ofertante_origem_obrigatoria",
                "Identificador de origem da unidade ofertante é obrigatório")),

        new(OfertaCursoErrorCodes.UnidadeOfertanteSiglaObrigatoria,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.unidade_ofertante_sigla_obrigatoria",
                "Sigla da unidade ofertante é obrigatória")),

        new(OfertaCursoErrorCodes.UnidadeOfertanteSiglaTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.unidade_ofertante_sigla_tamanho",
                "Tamanho da sigla da unidade ofertante inválido")),

        new(OfertaCursoErrorCodes.UnidadeOfertanteNomeObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.unidade_ofertante_nome_obrigatorio",
                "Nome da unidade ofertante é obrigatório")),

        new(OfertaCursoErrorCodes.UnidadeOfertanteNomeTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.unidade_ofertante_nome_tamanho",
                "Tamanho do nome da unidade ofertante inválido")),

        new(OfertaCursoErrorCodes.UnidadeOfertanteTipoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.unidade_ofertante_tipo_obrigatorio",
                "Tipo da unidade ofertante é obrigatório")),

        new(OfertaCursoErrorCodes.UnidadeOfertanteTipoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.unidade_ofertante_tipo_tamanho",
                "Tamanho do tipo da unidade ofertante inválido")),

        new(OfertaCursoErrorCodes.ProgramaDeOfertaInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.programa_de_oferta_invalido",
                "Programa de oferta não é um dos códigos aceitos")),

        new(OfertaCursoErrorCodes.FormatoPedagogicoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.formato_pedagogico_invalido",
                "Formato pedagógico não é um dos códigos aceitos")),

        new(OfertaCursoErrorCodes.TurnoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.turno_invalido",
                "Turno não é um dos códigos aceitos")),

        new(OfertaCursoErrorCodes.RegimeDeTurnoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.regime_de_turno_obrigatorio",
                "Regime de turno é obrigatório")),

        new(OfertaCursoErrorCodes.RegimeDeTurnoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.regime_de_turno_invalido",
                "Regime de turno não é um dos códigos aceitos")),

        new(OfertaCursoErrorCodes.TurnosObrigatorios,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.turnos_obrigatorios",
                "A oferta deve declarar seus turnos")),

        new(OfertaCursoErrorCodes.TurnoRepetido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.turno_repetido",
                "Os turnos da oferta devem ser distintos entre si")),

        new(OfertaCursoErrorCodes.CardinalidadeTurnosIncompativelComRegime,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.cardinalidade_turnos_incompativel_com_regime",
                "Quantidade de turnos incompatível com o regime declarado")),

        new(OfertaCursoErrorCodes.RegimeDeFuncionamentoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.regime_de_funcionamento_obrigatorio",
                "Regime de funcionamento é obrigatório")),

        new(OfertaCursoErrorCodes.RegimeDeFuncionamentoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.regime_de_funcionamento_invalido",
                "Regime de funcionamento não é um dos códigos aceitos")),

        new(OfertaCursoErrorCodes.RegimeDeFuncionamentoIncompativelComRegimeDeTurno,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.regime_de_funcionamento_incompativel_com_regime_de_turno",
                "Regime de funcionamento incompatível com o regime de turno declarado")),

        new(OfertaCursoErrorCodes.BaseLegalObrigatoriaParaProgramaNaoRegular,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.base_legal_obrigatoria_para_programa_nao_regular",
                "Base legal é obrigatória quando o programa de oferta não é Regular")),

        new(OfertaCursoErrorCodes.VagasAnuaisNegativas,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.vagas_anuais_negativas",
                "Vagas anuais autorizadas não pode ser negativo")),

        new(OfertaCursoErrorCodes.CodigoEmecTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.codigo_emec_tamanho",
                "Tamanho do código do e-MEC inválido")),

        new(OfertaCursoErrorCodes.CodigoNoSistemaAcademicoTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.codigo_no_sistema_academico_tamanho",
                "Tamanho do código no sistema acadêmico inválido")),

        new(OfertaCursoErrorCodes.BaseLegalTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.base_legal_tamanho",
                "Tamanho da base legal da oferta de curso inválido")),

        new(OfertaCursoErrorCodes.AtoAutorizacaoMecTamanho,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.ato_autorizacao_mec_tamanho",
                "Tamanho do ato de autorização MEC inválido")),

        new(OfertaCursoErrorCodes.CodigoObrigatorio,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.codigo_obrigatorio",
                "O código da oferta de curso é obrigatório")),

        new(OfertaCursoErrorCodes.CodigoFormatoInvalido,
            new DomainErrorMapping(
                StatusCodes.Status422UnprocessableEntity,
                "uniplus.configuracao.oferta_curso.codigo_formato_invalido",
                "O código da oferta de curso está fora do formato")),

        new(OfertaCursoErrorCodes.CodigoJaEmUso,
            new DomainErrorMapping(
                StatusCodes.Status409Conflict,
                "uniplus.configuracao.oferta_curso.codigo_ja_em_uso",
                "Já existe uma oferta de curso ativa com este código")),

        new(OfertaCursoErrorCodes.NaoEncontrada,
            new DomainErrorMapping(
                StatusCodes.Status404NotFound,
                "uniplus.configuracao.oferta_curso.nao_encontrada",
                "Oferta de curso não encontrada")),
    ];
}
