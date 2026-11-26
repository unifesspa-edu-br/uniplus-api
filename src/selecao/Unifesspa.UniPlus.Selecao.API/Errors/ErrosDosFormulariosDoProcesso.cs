namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;

/// <summary>
/// Recusas dos formulários do processo: código, fase, exigências de documento, aplicação de modelo,
/// obrigatoriedade por fase, completude do conjunto de inscrição e termos exigidos. A ordem vale: o registro global guarda o último mapeamento de um código repetido.
/// </summary>
internal static class ErrosDosFormulariosDoProcesso
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        .. CodigoEExigenciasDosFormularios(),
        .. FormulariosDoProcesso(),
        .. AplicacaoDeModelo(),
        .. CompletudeDosFormularios(),
        .. TermosExigidos(),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> CodigoEExigenciasDosFormularios() =>
    [
        new(CodigoDoFormularioErrorCodes.Obrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formulario_processo.codigo_obrigatorio", "O código do formulário é obrigatório")),
        new(CodigoDoFormularioErrorCodes.FormatoInvalido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formulario_processo.codigo_formato_invalido", "O código do formulário não tem o formato permitido")),
        new(FormulariosDeclaradosErrorCodes.CodigoDuplicado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formulario_processo.codigo_duplicado", "O código do formulário já identifica outro formulário do processo")),
        new(FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioInexistente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formulario_processo.exigencia_em_formulario_inexistente", "O processo não tem o formulário citado pela exigência de documento")),
        new(FormulariosDeclaradosErrorCodes.ExigenciaEmFormularioDeOutraFase, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formulario_processo.exigencia_em_formulario_de_outra_fase", "O formulário citado é de outra fase que a da exigência de documento")),
        new(FormulariosDeclaradosErrorCodes.RemocaoDeFormularioComExigencia, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formulario_processo.remocao_de_formulario_com_exigencia", "O formulário entrega exigência de documento e não pode ser removido")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> FormulariosDoProcesso() =>
    [
        new("FormularioProcesso.FaseForaDoCronograma", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formulario_processo.fase_fora_do_cronograma", "A fase do formulário não está no cronograma do processo")),
        new("FormularioProcesso.IsencaoSemTaxa", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formulario_processo.isencao_sem_taxa", "O processo que não cobra taxa tem formulário na fase de solicitação de isenção")),
        new(FormularioProcessoErrorCodes.NaoEncontrado, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.formulario_processo.nao_encontrado", "Formulário do processo não encontrado")),
        new(FormularioProcessoErrorCodes.FormularioInexistente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formulario_processo.formulario_inexistente", "O processo não tem formulário com esse código")),
        new(FormularioProcessoErrorCodes.ConsequenciaDaFaltaDeEnvioInvalida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formulario_processo.consequencia_da_falta_de_envio_invalida", "A consequência da falta de envio do formulário não é uma opção conhecida")),
        new(FormularioProcessoErrorCodes.ConsequenciaEmFormularioFacultativo, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formulario_processo.consequencia_em_formulario_facultativo", "O formulário facultativo não declara consequência para a falta de envio")),
        new(FormularioProcessoErrorCodes.ConsequenciaEmFormularioDaInscricao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formulario_processo.consequencia_em_formulario_da_inscricao", "O formulário que coleta a inscrição não declara consequência para a falta de envio")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> AplicacaoDeModelo() =>
    [
        new("AplicacaoDeModelo.ModeloInexistente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.aplicacao_de_modelo.modelo_inexistente", "O modelo de formulário informado não existe")),
        new("AplicacaoDeModelo.ModeloInativo", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.aplicacao_de_modelo.modelo_inativo", "O modelo de formulário está desativado")),
        new("AplicacaoDeModelo.TipoDeProcessoDiferente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.aplicacao_de_modelo.tipo_de_processo_diferente", "O modelo é de outro tipo de processo")),
        new("AplicacaoDeModelo.PressupostoAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.aplicacao_de_modelo.pressuposto_ausente", "O modelo cita fato que a inscrição do processo não coleta")),
        new("AplicacaoDeModelo.SemDistribuicaoDeVagas", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.aplicacao_de_modelo.sem_distribuicao_de_vagas", "O modelo cita a modalidade, e o processo não tem distribuição de vagas")),
        new(AplicacaoDeModeloErrorCodes.SemCotaDaLeiOfertada, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.aplicacao_de_modelo.sem_cota_da_lei_ofertada", "O modelo cita a opção pelas cotas da lei, e o processo não oferta cota da lei")),
        new(AplicacaoDeModeloErrorCodes.FaseDoDocumentoInexistente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.aplicacao_de_modelo.fase_do_documento_inexistente", "O modelo exige documento em fase que o cronograma do processo não tem")),
        new(AplicacaoDeModeloErrorCodes.TipoDeDocumentoInexistente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.aplicacao_de_modelo.tipo_de_documento_inexistente", "O modelo exige tipo de documento que o catálogo não tem")),
        new("AplicacaoDeModelo.DerivadoSemRegra", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.aplicacao_de_modelo.derivado_sem_regra", "O modelo cita derivado sem regra no processo nem no catálogo")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> CompletudeDosFormularios() =>
    [
        new("ProcessoSeletivo.InscricaoSemConjuntoBasico", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.inscricao_sem_conjunto_basico", "O conjunto básico de dados do candidato não está todo em formulário obrigatório da fase que coleta inscrição")),
        new("ProcessoSeletivo.InscricaoSemEdicaoDoEnem", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.inscricao_sem_edicao_do_enem", "A classificação usa as notas do ENEM, e o formulário de inscrição não pede a edição a todo candidato")),
        new("ProcessoSeletivo.EdicaoDoEnemSemClassificacaoEnem", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.edicao_do_enem_sem_classificacao_enem", "Um formulário pede a edição do ENEM, e a classificação não usa as notas do ENEM")),
        new(FormularioProcessoErrorCodes.InscricaoSemFormularioObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formulario_processo.inscricao_sem_formulario_obrigatorio", "O processo com inscrição pelo sistema não tem formulário obrigatório na fase que coleta inscrição")),
        new(FormularioProcessoErrorCodes.InscricaoEmResultadoImportado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formulario_processo.inscricao_em_resultado_importado", "O processo de resultado importado tem formulário na fase que coleta inscrição")),
        new(FormularioProcessoErrorCodes.FormularioDaIsencaoObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formulario_processo.formulario_da_isencao_obrigatorio", "Formulário obrigatório na fase de solicitação de isenção que não coleta inscrição")),
        new(FormularioProcessoErrorCodes.TaxaSemFormularioDeIsencao, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formulario_processo.taxa_sem_formulario_de_isencao", "O processo que cobra taxa não tem formulário facultativo na fase de solicitação de isenção")),
        new(FormularioProcessoErrorCodes.PosImportacaoSemFormularioObrigatorio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.formulario_processo.pos_importacao_sem_formulario_obrigatorio", "A primeira fase depois da importação não tem formulário obrigatório")),
        new("ProcessoSeletivo.FaseReferenciadaPorFormulario", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.fase_referenciada_por_formulario", "A fase é usada por um formulário e não pode sair do cronograma")),
        new("ProcessoSeletivo.FormularioSemFase", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.formulario_sem_fase", "O formulário ainda não declara a fase em que é respondido")),
        new("ProcessoSeletivo.ItemDoFormularioForaDeSecao", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.item_do_formulario_fora_de_secao", "Item do formulário fora de uma seção ou da ordem das seções")),
    ];

    private static IEnumerable<KeyValuePair<string, DomainErrorMapping>> TermosExigidos() =>
    [
        new("TermoExigidoFormulario.ObrigatoriedadeInvalida", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.termo_exigido_formulario.obrigatoriedade_invalida", "A obrigatoriedade do termo é SEMPRE ou NUNCA sem predicado, ou QUANDO com predicado")),
        new("TermoExigidoFormulario.EntradaMalformada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.termo_exigido_formulario.entrada_malformada", "A lista de termos ou um dos termos veio nulo")),
        new("TermoExigidoFormulario.VersaoNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.termo_exigido_formulario.versao_nao_encontrada", "A versão não existe no catálogo de termos ou pertence a outro termo")),
        new("ProcessoSeletivo.TermoExigidoSemFormaDeAceite", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.processo_seletivo.termo_exigido_sem_forma_de_aceite", "Termo exigido usa versão sem forma de aceite definida")),
    ];
}
