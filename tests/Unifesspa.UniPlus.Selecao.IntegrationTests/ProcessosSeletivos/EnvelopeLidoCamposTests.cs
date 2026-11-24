namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

public sealed class EnvelopeLidoCamposTests
{
    [Theory]
    [InlineData("etapas.0.nome", "Prova Objetiva Reformulada")]
    [InlineData("etapas.0.peso", "9.8750")]
    [InlineData("etapas.0.notaMinima", "12.3400")]
    [InlineData("etapas.0.carater", "CLASSIFICATORIA")]
    [InlineData("etapas.1.tipoEtapa.notaDeOrigemNoEnem", "true")]
    [InlineData("etapas.2.tipoEtapa.exigeCorrecaoIndependente", "false")]
    [InlineData("periodo.numero", "099/2026")]
    [InlineData("periodo.inicio", "2026-03-09T03:00:00.000000Z")]
    [InlineData("periodo.fim", "2026-04-30T02:59:59.000000Z")]
    [InlineData("distribuicao.0.regraDistribuicao.versao", "v9")]
    [InlineData("distribuicao.0.referenciaDemografica.censoReferencia", "Censo IBGE 2010")]
    [InlineData("distribuicao.0.referenciaDemografica.baseLegal", "Lei 14.723/2023 art. 3º")]
    [InlineData("modalidades.0.baseLegal", "Outra base legal inteiramente diversa")]
    [InlineData("modalidades.0.descricao", "Outra descrição")]
    [InlineData("modalidades.1.acaoQuandoIndeferido|modalidades.10.acaoQuandoIndeferido", "RECLASSIFICAR_REGRA_EDITAL")]
    [InlineData("fada.fator", "1.9900")]
    [InlineData("fada.teto", "42.5000")]
    [InlineData("fada.identificacao", "Portaria Unifesspa nº 999/2026")]
    [InlineData("fada.descricao", "Descrição alterada")]
    [InlineData("opcoesDeCurso.numero", "1")]
    [InlineData("classificacao.casasArredondamento", "4")]
    [InlineData("classificacao.regraCalculo.hash", "9999999999999999999999999999999999999999999999999999999999999999")]
    [InlineData("atendimento.condicoes.0.condicaoNome", "Outra condição")]
    [InlineData("atendimento.recursos.0.recursoCodigo", "INTERPRETE_LIBRAS")]
    [InlineData("atendimento.recursos.0.recursoNome", "Intérprete de Libras")]
    [InlineData("atendimento.tiposDeficiencia.0.tipoDeficienciaNome", "Deficiência física")]
    [InlineData("identificadorLegivel", "outro-certame-2026")]
    public void Decoder_NaoPerdeCampo(string caminho, string valorNovo) =>
        FidelidadeDoEnvelopeDeTeste.ConferirMutacao(caminho, valorNovo, ordena: false);

    [Theory]
    [InlineData("etapas.0.ordem", "9")]
    [InlineData("criteriosDesempate.0.ordem", "9")]
    [InlineData("classificacao.regrasEliminacao.1.parametros.notaMinima", "88.7500")]
    [InlineData("classificacao.regrasEliminacao.0.parametros.minimo", "555.0000")]
    [InlineData("classificacao.regrasEliminacao.0.parametros.areaCodigo", "MATEMATICA")]
    [InlineData("modalidades.1.regraDeAplicabilidade.0.0.valor|modalidades.10.regraDeAplicabilidade.0.0.valor", "ATE_MEIO_SM")]
    public void Decoder_NaoPerdeCampoDeOrdenacao(string caminho, string valorNovo) =>
        FidelidadeDoEnvelopeDeTeste.ConferirMutacao(caminho, valorNovo, ordena: true);
}
