namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Enums;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

public sealed class CodigosDeContratoDoCatalogoDeRegrasTests
{
    [Fact]
    public void RegraDoFada_Valores_CodigosERotulosDeclarados()
    {
        Enum.GetValues<RegraDoFada>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (RegraDoFada.Multiplicativo, "FADA_MULTIPLICATIVO", "Multiplica a nota final pelo Fator de Atenuação das Desigualdades de Acesso (FADA)"));
    }

    [Fact]
    public void RegraDeArredondamentoDaNota_Valores_CodigosERotulosDeclarados()
    {
        Enum.GetValues<RegraDeArredondamentoDaNota>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (RegraDeArredondamentoDaNota.Truncar, "PRECISAO_TRUNCAR", "Trunca a nota na casa decimal definida"),
                (RegraDeArredondamentoDaNota.ArredondarAPartirDeCinco, "PRECISAO_ARREDONDAR_A_PARTIR_DE_CINCO", "Arredonda a nota a partir de cinco"));
    }

    [Fact]
    public void RegraDeCalculoDaNota_Valores_CodigosERotulosDeclarados()
    {
        Enum.GetValues<RegraDeCalculoDaNota>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (RegraDeCalculoDaNota.MediaPonderada, "FORMULA_MEDIA_PONDERADA", "Calcula a média ponderada"),
                (RegraDeCalculoDaNota.ClassificacaoImportada, "CLASSIFICACAO_IMPORTADA", "Usa a classificação importada"));
    }

    [Fact]
    public void RegraDeOrdemDeAlocacao_Valores_CodigosERotulosDeclarados()
    {
        Enum.GetValues<RegraDeOrdemDeAlocacao>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria, "ALOCACAO_PRIMEIRA_OPCAO_PRIORITARIA", "Prioriza a primeira opção de curso"));
    }

    [Fact]
    public void RegraDeAjusteDaDistribuicaoDeVagas_Valores_CodigosERotulosDeclarados()
    {
        Enum.GetValues<RegraDeAjusteDaDistribuicaoDeVagas>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (RegraDeAjusteDaDistribuicaoDeVagas.AjusteDoTotalPeloArtigo11, "AJUSTE_VAGAS_ARTIGO_11_PARAGRAFO_UNICO", "Ajusta a distribuição conforme o artigo 11, parágrafo único"));
    }

    [Fact]
    public void RegraDeDesempate_Valores_CodigosERotulosDeclarados()
    {
        Enum.GetValues<RegraDeDesempate>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (RegraDeDesempate.MaiorNotaNaEtapa, "DESEMPATE_MAIOR_NOTA_ETAPA", "Prioriza a maior nota na etapa"),
                (RegraDeDesempate.MaiorIdade, "DESEMPATE_MAIOR_IDADE", "Prioriza a maior idade"),
                (RegraDeDesempate.Idoso, "DESEMPATE_IDOSO", "Prioriza a pessoa idosa"),
                (RegraDeDesempate.CondicaoSobreDado, "DESEMPATE_CONDICAO_SOBRE_DADO", "Prioriza quem atende a uma condição"),
                (RegraDeDesempate.MaiorNotaPorAreaDoEnem, "DESEMPATE_MAIOR_NOTA_AREA_ENEM", "Prioriza a maior nota por área do ENEM"));
    }

    [Fact]
    public void RegraDeEliminacaoPorCalculo_Valores_CodigosERotulosDeclarados()
    {
        Enum.GetValues<RegraDeEliminacaoPorCalculo>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa, "ELIMINACAO_NOTA_MINIMA_ETAPA", "Elimina quem fica abaixo da nota mínima na etapa"),
                (RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem, "ELIMINACAO_CORTE_EM_AREA", "Elimina quem fica abaixo do mínimo numa área do ENEM"),
                (RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem, "ELIMINACAO_ZERO_EM_AREA", "Elimina quem tira zero em alguma área do ENEM"),
                (RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem, "ELIMINACAO_FALTA_EM_DIA_DE_PROVA_ENEM", "Elimina quem falta a um dia de prova do ENEM"),
                (RegraDeEliminacaoPorCalculo.AusenciaNaEtapa, "ELIMINACAO_AUSENCIA_NA_ETAPA", "Elimina quem falta à etapa"));
    }
    [Fact]
    public void ConvencaoDeContagemDoPrazo_Valores_CodigosERotulosDeclarados() =>
        Enum.GetValues<ConvencaoDeContagemDoPrazo>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (ConvencaoDeContagemDoPrazo.ExcluiDiaInicial, "CONTAGEM_PRAZO_EXCLUI_DIA_INICIAL", "Exclui o dia inicial"),
                (ConvencaoDeContagemDoPrazo.HorasUteisDesdeOEventoInicial, "CONTAGEM_PRAZO_HORAS_UTEIS_DESDE_O_EVENTO_INICIAL", "Conta horas úteis desde o evento inicial"),
                (ConvencaoDeContagemDoPrazo.AvancaDataUtil, "CONTAGEM_PRAZO_AVANCA_DATA_UTIL", "Avança para a data útil"));

    [Fact]
    public void RegraDePrazoDoRecurso_Valores_CodigosERotulosDeclarados() =>
        Enum.GetValues<RegraDePrazoDoRecurso>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal((RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto, "RECURSO_PRAZO_CONTADO_DA_PUBLICACAO_DO_ATO", "Conta o prazo da publicação do ato"));
    [Fact]
    public void RegraDeApuracaoDaAvaliacao_Valores_CodigosERotulosDeclarados() =>
        Enum.GetValues<RegraDeApuracaoDaAvaliacao>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (RegraDeApuracaoDaAvaliacao.AvaliacaoUnica, "APURACAO_AVALIACAO_UNICA", "Usa a avaliação de um avaliador"),
                (RegraDeApuracaoDaAvaliacao.CorrecaoIndependente, "APURACAO_CORRECAO_INDEPENDENTE", "Combina correções independentes"),
                (RegraDeApuracaoDaAvaliacao.ConceitoUnanimidade, "APURACAO_CONCEITO_UNANIMIDADE", "Apura o conceito por unanimidade"),
                (RegraDeApuracaoDaAvaliacao.ConceitoMaioriaSimples, "APURACAO_CONCEITO_MAIORIA_SIMPLES", "Apura o conceito por maioria simples"),
                (RegraDeApuracaoDaAvaliacao.ConceitoMaioriaAbsoluta, "APURACAO_CONCEITO_MAIORIA_ABSOLUTA", "Apura o conceito por maioria absoluta"));

    [Fact]
    public void CriterioDeRemanejamento_Valores_CodigosERotulosDeclarados() =>
        Enum.GetValues<CriterioDeRemanejamento>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal((CriterioDeRemanejamento.OrdemDaLei12711, "REMANEJAMENTO_ORDEM_LEI_12711", "Segue a ordem da Lei 12.711"));
    [Fact]
    public void RegraDeProcedimentoDoRecurso_Valores_CodigosERotulosDeclarados() =>
        Enum.GetValues<RegraDeProcedimentoDoRecurso>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (RegraDeProcedimentoDoRecurso.Recorrecao, "PROCEDIMENTO_RECURSO_RECORRECAO", "Nova correção"),
                (RegraDeProcedimentoDoRecurso.Conferencia, "PROCEDIMENTO_RECURSO_CONFERENCIA", "Conferência"),
                (RegraDeProcedimentoDoRecurso.CorrecaoMaterial, "PROCEDIMENTO_RECURSO_CORRECAO_MATERIAL", "Correção de registro"),
                (RegraDeProcedimentoDoRecurso.Recalculo, "PROCEDIMENTO_RECURSO_RECALCULO", "Novo cálculo"));
    [Fact]
    public void RegraDeDistribuicaoDeVagas_Valores_CodigosERotulosDeclarados() =>
        Enum.GetValues<RegraDeDistribuicaoDeVagas>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (RegraDeDistribuicaoDeVagas.Lei12711, "DISTRIBUICAO_LEI_12711", "Distribuição pela Lei 12.711"),
                (RegraDeDistribuicaoDeVagas.Lei12711ComAmplaConcorrenciaEPessoaComDeficiencia, "DISTRIBUICAO_LEI_12711_COM_AMPLA_CONCORRENCIA_E_PESSOA_COM_DEFICIENCIA", "Lei 12.711 com ampla concorrência e pessoa com deficiência"),
                (RegraDeDistribuicaoDeVagas.Institucional, "DISTRIBUICAO_INSTITUCIONAL", "Distribuição institucional"),
                (RegraDeDistribuicaoDeVagas.PorAcrescimoIndigenaEQuilombola, "DISTRIBUICAO_POR_ACRESCIMO_INDIGENA_E_QUILOMBOLA", "Vagas por acréscimo indígena e quilombola"),
                (RegraDeDistribuicaoDeVagas.ComAmplaConcorrenciaEPessoaComDeficiencia, "DISTRIBUICAO_COM_AMPLA_CONCORRENCIA_E_PESSOA_COM_DEFICIENCIA", "Ampla concorrência e pessoa com deficiência"));

    [Fact]
    public void MontagemDoQuadroDeVagas_Valores_CodigosERotulosDeclarados() =>
        Enum.GetValues<ModoDeMontagemDoQuadroDeVagas>().Select(static valor => (valor, valor.Codigo(), valor.Rotulo()))
            .Should().Equal(
                (ModoDeMontagemDoQuadroDeVagas.LeiDeCotas, "LEI_DE_COTAS", "Cálculo pela Lei de Cotas"),
                (ModoDeMontagemDoQuadroDeVagas.QuadroFixadoNoEdital, "QUADRO_FIXADO_NO_EDITAL", "Quadro fixado no edital"));
}
