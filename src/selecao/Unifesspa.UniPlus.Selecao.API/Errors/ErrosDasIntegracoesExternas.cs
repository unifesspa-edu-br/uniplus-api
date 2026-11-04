namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Recusas da conferência de endereço no Geo e da importação das notas do ENEM.
/// </summary>
internal static class ErrosDasIntegracoesExternas
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        // Conferência do endereço do candidato no Geo (issue #1865, ADR-0136).
        new(ConferenciaEnderecoGeoErrorCodes.CepNaoResolve, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.conferencia_endereco_geo.cep_nao_resolve", "O CEP informado não tem cobertura no Geo")),
        new(ConferenciaEnderecoGeoErrorCodes.CidadeDivergente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.conferencia_endereco_geo.cidade_divergente", "O CEP informado resolve para uma cidade diferente da declarada")),
        new(ConferenciaEnderecoGeoErrorCodes.MunicipioNaoEncontrado, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.conferencia_endereco_geo.municipio_nao_encontrado", "O município informado não existe no Geo")),
        new(ConferenciaEnderecoGeoErrorCodes.MunicipioDivergente, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.conferencia_endereco_geo.municipio_divergente", "O município informado não pertence à UF declarada")),
        // Falha fechada: o Geo não respondeu — temporário, nunca "aceito".
        new(ConferenciaEnderecoGeoErrorCodes.GeoIndisponivel, new DomainErrorMapping(StatusCodes.Status503ServiceUnavailable, "uniplus.selecao.conferencia_endereco_geo.geo_indisponivel", "O Geo não respondeu — a submissão é recusada, mas o rascunho continua salvável")),
        // Envio ao INEP e importação das notas do ENEM (issue #1602, ADR-0148 e ADR-0149).
        new(NotasDoEnemErrorCodes.EtapaNaoEncontrada, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.notas_do_enem.etapa_nao_encontrada", "A etapa não pertence ao processo seletivo")),
        new(NotasDoEnemErrorCodes.EtapaSemNotaDoEnem, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.notas_do_enem.etapa_sem_nota_do_enem", "A etapa não tem a nota vinda do ENEM")),
        new(NotasDoEnemErrorCodes.EdicaoNaoAdmitida, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.notas_do_enem.edicao_nao_admitida", "A edição do ENEM não é uma das aceitas pelo processo")),
        new(NotasDoEnemErrorCodes.LeiauteIndisponivel, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.notas_do_enem.leiaute_indisponivel", "O leiaute escolhido não está disponível")),
        new(NotasDoEnemErrorCodes.EdicaoSemInscritos, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.notas_do_enem.edicao_sem_inscritos", "Nenhuma inscrição enviada escolheu esta edição")),
        new(NotasDoEnemErrorCodes.ArquivoVazio, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.notas_do_enem.arquivo_vazio", "O arquivo não foi enviado ou está vazio")),
        new(NotasDoEnemErrorCodes.ArquivoGrandeDemais, new DomainErrorMapping(StatusCodes.Status413PayloadTooLarge, "uniplus.selecao.notas_do_enem.arquivo_grande_demais", "O arquivo passa do tamanho aceito")),
        new(NotasDoEnemErrorCodes.TipoDeArquivoNaoAceito, new DomainErrorMapping(StatusCodes.Status415UnsupportedMediaType, "uniplus.selecao.notas_do_enem.tipo_de_arquivo_nao_aceito", "O tipo do arquivo não é aceito")),
        new(NotasDoEnemErrorCodes.LoteNaoEncontrado, new DomainErrorMapping(StatusCodes.Status404NotFound, "uniplus.selecao.notas_do_enem.lote_nao_encontrado", "O lote de notas não foi encontrado")),
        new(NotasDoEnemErrorCodes.ArquivoForaDoLeiaute, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.notas_do_enem.arquivo_fora_do_leiaute", "O arquivo não confere com o leiaute escolhido")),
        new(NotasDoEnemErrorCodes.CodigoDeSituacaoDesconhecido, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.notas_do_enem.codigo_de_situacao_desconhecido", "O arquivo traz um código de situação que o leiaute não conhece")),
        new(NotasDoEnemErrorCodes.InscricoesEmAndamento, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.notas_do_enem.inscricoes_em_andamento", "As inscrições do processo ainda não terminaram")),
        new(NotasDoEnemErrorCodes.OfertaDeInscricaoForaDoEdital, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.notas_do_enem.oferta_de_inscricao_fora_do_edital", "Há inscrição que concorre a um curso que não está no edital divulgado")),
    ];
}
