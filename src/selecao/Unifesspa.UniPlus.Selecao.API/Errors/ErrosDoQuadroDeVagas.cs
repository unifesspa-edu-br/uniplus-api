namespace Unifesspa.UniPlus.Selecao.API.Errors;

using Microsoft.AspNetCore.Http;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

/// <summary>
/// Recusas do quadro de vagas (ADR-0115): cálculo do ramo federal (Lei 12.711), fixação do ramo institucional e a regra de ajuste (art. 11 parágrafo único) congelada junto da configuração.
/// </summary>
internal static class ErrosDoQuadroDeVagas
{
    internal static IEnumerable<KeyValuePair<string, DomainErrorMapping>> Mapeamentos() =>
    [
        new("ConfiguracaoDistribuicaoVagas.QuadroAmplaConcorrenciaNegativa", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.quadro_ampla_concorrencia_negativa", "A ampla concorrência resultaria negativa — ajuste as vagas ofertadas base, o percentual da reserva legal ou as retiradas")),
        new(DistribuicaoDeVagasErrorCodes.QuantidadeVagaNegativa, new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.quantidade_vaga_negativa", "A quantidade de vagas de uma modalidade não pode ser negativa")),
        new("ConfiguracaoDistribuicaoVagas.RegraAjusteObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.regra_ajuste_obrigatoria", "A distribuição pela Lei 12.711 exige a regra de ajuste (art. 11 §único)")),
        new("ConfiguracaoDistribuicaoVagas.QuadroModalidadeAusente", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.quadro_modalidade_ausente", "Uma modalidade selecionada não tem quantidade fixada pelo edital")),
        new("ConfiguracaoDistribuicaoVagas.QuantidadeCalculadaNaoInformavel", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.quantidade_calculada_nao_informavel", "A quantidade de uma modalidade calculada pela Lei 12.711 não pode ser fixada pelo edital")),
        new("ConfiguracaoDistribuicaoVagas.QuantidadeDeclaradaObrigatoria", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.quantidade_declarada_obrigatoria", "A modalidade de retirada ou suplemento exige a quantidade fixada pelo edital")),
        new("ConfiguracaoDistribuicaoVagas.RegraAjusteNaoEncontrada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.regra_ajuste_nao_encontrada", "Regra de ajuste não encontrada no catálogo de regras")),
        new("ConfiguracaoDistribuicaoVagas.RegraAjusteTipoInvalido", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.regra_ajuste_tipo_invalido", "A regra referenciada não é uma regra de ajuste da distribuição de vagas")),
        new("ConfiguracaoDistribuicaoVagas.QuadroModalidadeNaoSelecionada", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.quadro_modalidade_nao_selecionada", "O quadro traz uma modalidade que não está selecionada nesta oferta")),
        new("ConfiguracaoDistribuicaoVagas.RetiradaFederalDeveSerDeAmplaConcorrencia", new DomainErrorMapping(StatusCodes.Status422UnprocessableEntity, "uniplus.selecao.configuracao_distribuicao_vagas.retirada_federal_deve_ser_de_ampla_concorrencia", "No ramo federal, toda modalidade de retirada só pode retirar da ampla concorrência")),
    ];
}
