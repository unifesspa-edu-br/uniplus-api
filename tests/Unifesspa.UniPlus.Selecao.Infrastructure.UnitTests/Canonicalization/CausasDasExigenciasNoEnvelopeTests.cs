namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CausasDasExigenciasNoEnvelopeTests
{
    [Fact]
    public void LerTaxaInscricao_SemValorEFundamentos_PropagaAmbas()
    {
        JsonObject documento = JsonNode.Parse("""{"taxaInscricao":{"presente":true,"cobra":true,"valor":null,"fundamentos":[]}}""")!.AsObject();
        LeitorEnvelope leitor = new();
        LeituraDaDivulgacaoETaxa.LerTaxaInscricao(leitor, documento);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().HaveCount(2);
        ConferirCausaNoDocumento.Conferir(documento, causas[0], "taxaInscricao.valor", "ConfiguracaoTaxaInscricao.ValorObrigatorioQuandoCobra", "Processo que cobra taxa exige valor positivo.");
        ConferirCausaNoDocumento.Conferir(documento, causas[1], "taxaInscricao.fundamentos", "ConfiguracaoTaxaInscricao.FundamentoObrigatorioQuandoCobra", "Processo que cobra taxa de inscrição exige ao menos um fundamento de isenção.");
    }

    [Fact]
    public void LerTaxaInscricao_ValorSemCobranca_PropagaCausa()
    {
        JsonObject documento = JsonNode.Parse("""{"taxaInscricao":{"presente":true,"cobra":false,"valor":"1.00","fundamentos":[]}}""")!.AsObject();
        LeitorEnvelope leitor = new();
        LeituraDaDivulgacaoETaxa.LerTaxaInscricao(leitor, documento);
        ConferirCausaNoDocumento.Conferir(documento, leitor.Falha<object>().Errors.Single(), "taxaInscricao.valor", "ConfiguracaoTaxaInscricao.ValorNaoPermitidoQuandoNaoCobra", "Processo que declara não cobrar taxa não pode informar valor.");
    }

    [Fact]
    public void LerDivulgacao_CampoDesconhecido_PropagaCausasSemRefletirValor()
    {
        const string valor = "VALOR_FICTICIO_X";
        JsonObject documento = JsonNode.Parse("""{"divulgacao":{"camposPublicos":["VALOR_FICTICIO_X"],"regraNomeAbreviado":null,"justificativa":null}}""")!.AsObject();
        LeitorEnvelope leitor = new();
        LeituraDaDivulgacaoETaxa.LerDivulgacao(leitor, documento);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().HaveCount(2);
        ConferirCausaNoDocumento.Conferir(documento, causas[0], "divulgacao.camposPublicos", "ConfiguracaoDivulgacao.CampoNaoPermitido", "Um ou mais campos não pertencem ao vocabulário de divulgação pública (NUMERO_INSCRICAO, NOME_ABREVIADO, NOME).");
        ConferirCausaNoDocumento.Conferir(documento, causas[1], "divulgacao.camposPublicos", "ConfiguracaoDivulgacao.NumeroInscricaoObrigatorio", "'NUMERO_INSCRICAO' é o piso da divulgação pública — não pode ser removido da lista.");
        causas.Should().OnlyContain(causa => !causa.Field!.Contains(valor) && !causa.Error.Message.Contains(valor));
    }

    [Fact]
    public void LerDivulgacao_ListaVazia_ApontaCamposPublicos()
    {
        JsonObject documento = JsonNode.Parse("""{"divulgacao":{"camposPublicos":[],"regraNomeAbreviado":null,"justificativa":null}}""")!.AsObject();
        LeitorEnvelope leitor = new();
        LeituraDaDivulgacaoETaxa.LerDivulgacao(leitor, documento);
        ConferirCausaNoDocumento.Conferir(documento, leitor.Falha<object>().Errors.Single(), "divulgacao.camposPublicos", "ConfiguracaoDivulgacao.CamposPublicosVazio", "A lista de campos públicos não pode ser vazia — para restaurar o default, remova a configuração explícita.");
    }

    [Fact]
    public void LerFormatosPermitidos_ListaVazia_ApontaObjeto()
    {
        JsonObject documento = JsonNode.Parse("""{"exigencia":{"formatosPermitidos":{"qualquer":false,"lista":[]}}}""")!.AsObject();
        LeitorEnvelope leitor = new();
        LeituraDosFormatosEBasesLegais.LerFormatosPermitidos(leitor, documento["exigencia"]!.AsObject(), "exigencia");
        ConferirCausaNoDocumento.Conferir(documento, leitor.Falha<object>().Errors.Single(), "exigencia.formatosPermitidos", "FormatosPermitidos.Obrigatorio", "FormatosPermitidos é obrigatório: declare QUALQUER ou uma lista com ao menos um formato.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LerBaseLegal_ReferenciaVazia_RecusaFormaAntesDaFabrica(bool comprovacao)
    {
        JsonObject documento = JsonNode.Parse("""{"item":{"basesLegais":[{"referencia":"","abrangencia":"NACIONAL","situacao":"RESOLVIDO","observacao":null}]}}""")!.AsObject();
        JsonObject item = documento["item"]!.AsObject();
        LeitorEnvelope leitor = new();
        if (comprovacao)
        {
            LeituraDasRegrasDeComprovacao.LerBasesLegaisDaRegra(leitor, item, "item");
        }
        else
        {
            LeituraDosFormatosEBasesLegais.LerBasesLegais(leitor, item, "item");
        }
        FieldError causa = leitor.Falha<object>().Errors.Single();
        causa.Should().Be(new FieldError(null, new DomainError("EnvelopeCodec.EnvelopeMalformado", "Envelope malformado em 'item.basesLegais[0].referencia': esperado um texto não vazio.")));
        CaminhoNoDocumento.Existe(documento, "item.basesLegais[0].referencia").Should().BeTrue();
    }

    [Fact]
    public void LerAtendimento_CondicoesDuplicadas_ApontaObjeto()
    {
        JsonObject documento = JsonNode.Parse("""
            {"atendimento":{"condicoes":[{"condicaoOrigemId":"11111111-1111-1111-1111-111111111111","condicaoCodigo":"CONDICAO","condicaoNome":"Condição"},
            {"condicaoOrigemId":"11111111-1111-1111-1111-111111111111","condicaoCodigo":"CONDICAO","condicaoNome":"Condição"}],"recursos":[],"tiposDeficiencia":[]}}
            """)!.AsObject();
        LeitorEnvelope leitor = new();
        LeituraDoAtendimento.LerAtendimento(leitor, documento);
        ConferirCausaNoDocumento.Conferir(documento, leitor.Falha<object>().Errors.Single(), "atendimento", "AtendimentoEspecializado.CondicaoDuplicada", "Cada condição de atendimento só pode ser ofertada uma vez.");
    }

    [Fact]
    public void LerAtendimento_TiposSemCondicaoEDuplicados_PropagaAmbas()
    {
        JsonObject documento = JsonNode.Parse("""
            {"atendimento":{"condicoes":[],"recursos":[],"tiposDeficiencia":[
            {"tipoDeficienciaOrigemId":"11111111-1111-1111-1111-111111111111","tipoDeficienciaCodigo":"TIPO","tipoDeficienciaNome":"Tipo"},
            {"tipoDeficienciaOrigemId":"11111111-1111-1111-1111-111111111111","tipoDeficienciaCodigo":"TIPO","tipoDeficienciaNome":"Tipo"}]}}
            """)!.AsObject();
        LeitorEnvelope leitor = new();
        LeituraDoAtendimento.LerAtendimento(leitor, documento);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().HaveCount(2);
        ConferirCausaNoDocumento.Conferir(documento, causas[0], "atendimento", "AtendimentoEspecializado.TipoDeficienciaDuplicado", "Cada tipo de deficiência só pode ser ofertado uma vez.");
        ConferirCausaNoDocumento.Conferir(documento, causas[1], "atendimento", "AtendimentoEspecializado.TipoDeficienciaSemCondicaoPcd", "Tipos de deficiência só podem ser ofertados quando a condição PcD está ofertada.");
    }

    [Fact]
    public void LerFada_FatorInvalidoESemMunicipios_PropagaAmbas()
    {
        JsonObject documento = DocumentoDaFada();
        LeitorEnvelope leitor = new();
        LeituraDoFada.LerFada(leitor, documento);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().HaveCount(2);
        ConferirCausaNoDocumento.Conferir(documento, causas[0], "fada.fator", "ConfiguracaoDoFada.FatorInvalido", "O multiplicador do Fator de Atenuação das Desigualdades de Acesso (FADA) deve ser maior que zero.");
        ConferirCausaNoDocumento.Conferir(documento, causas[1], "fada", "ConfiguracaoDoFada.SemMunicipios", "A base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) deve abranger pelo menos um município.");
    }
    [Fact]
    public void LerFada_SemMunicipios_ApontaObjeto()
    {
        JsonObject documento = DocumentoDaFada();
        documento["fada"]!["fator"] = "1.2000";
        LeitorEnvelope leitor = new();
        LeituraDoFada.LerFada(leitor, documento);
        ConferirCausaNoDocumento.Conferir(documento, leitor.Falha<object>().Errors.Single(), "fada", "ConfiguracaoDoFada.SemMunicipios", "A base legal do Fator de Atenuação das Desigualdades de Acesso (FADA) deve abranger pelo menos um município.");
    }

    private static JsonObject DocumentoDaFada() => JsonNode.Parse("""
        {"fada":{"presente":true,"regra":{"codigo":"FADA_MULTIPLICATIVO","versao":"v1","hash":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"},
        "fator":"0.0000","teto":null,"codigoDoDadoDeElegibilidade":null,"baseLegalId":"11111111-1111-1111-1111-111111111111",
        "baseLegalCodigo":"NORMA","tipoInstrumento":"RESOLUCAO","identificacao":"123","descricao":"Norma","municipios":[]}}
        """)!.AsObject();

}
