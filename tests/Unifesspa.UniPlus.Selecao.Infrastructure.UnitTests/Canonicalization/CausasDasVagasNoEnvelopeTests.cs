namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class CausasDasVagasNoEnvelopeTests
{
    [Fact]
    public void LerDistribuicao_VagasBaseInvalidas_ApontaCampo()
    {
        JsonObject documento = Documento();
        documento["distribuicao"]![0]!["vagasOfertadasBase"] = 0;
        LeitorEnvelope leitor = new();
        LeituraDasVagas.LerDistribuicao(leitor, documento);
        FieldError causa = leitor.Falha<object>().Errors.Single();
        ConferirCausaNoDocumento.Conferir(documento, causa, "distribuicao[0].vagasOfertadasBase", "ConfiguracaoDistribuicaoVagas.VagasOfertadasBaseInvalido", "As vagas ofertadas base devem ser maior que zero.");
        causa.Error.Message.Should().NotContain("VALOR_FICTICIO_X");
        causa.Field.Should().NotContain("VALOR_FICTICIO_X");
    }

    [Fact]
    public void LerDistribuicao_VagasInvalidasEModalidadeRepetida_PropagaAmbas()
    {
        JsonObject documento = Documento();
        documento["distribuicao"]![0]!["vagasOfertadasBase"] = 0;
        JsonArray modalidades = documento["modalidades"]!.AsArray();
        modalidades.Add(modalidades[0]!.DeepClone());
        LeitorEnvelope leitor = new();
        LeituraDasVagas.LerDistribuicao(leitor, documento);
        IReadOnlyList<FieldError> causas = leitor.Falha<object>().Errors;
        causas.Should().HaveCount(2);
        ConferirCausaNoDocumento.Conferir(documento, causas[0], "distribuicao[0].vagasOfertadasBase", "ConfiguracaoDistribuicaoVagas.VagasOfertadasBaseInvalido", "As vagas ofertadas base devem ser maior que zero.");
        ConferirCausaNoDocumento.Conferir(documento, causas[1], "distribuicao[0]", "ConfiguracaoDistribuicaoVagas.ModalidadeDuplicada", "Cada modalidade só pode ser selecionada uma vez por oferta.");
        causas.Should().OnlyContain(causa => !causa.Error.Message.Contains("VALOR_FICTICIO_X") && !causa.Field!.Contains("VALOR_FICTICIO_X"));
    }

    [Fact]
    public void LerDistribuicao_BlocosIncoerentes_MantemErroSemCampo()
    {
        JsonObject documento = Documento();
        documento["vagas"]!.AsArray().Clear();
        LeitorEnvelope leitor = new();
        LeituraDasVagas.LerDistribuicao(leitor, documento);
        FieldError causa = leitor.Falha<object>().Errors.Single();
        causa.Field.Should().BeNull();
        causa.Error.Should().Be(new DomainError("EnvelopeCodec.BlocosDerivadosIncoerentes", "Os blocos 'distribuicao', 'modalidades', 'vagas' e 'ofertas' derivam da mesma coleção e não declaram o mesmo conjunto de ofertas de curso."));
    }

    [Fact]
    public void LerDistribuicao_SegundaModalidadeIncoerente_ApontaSegundaPosicao()
    {
        JsonObject documento = Documento();
        JsonArray modalidades = documento["modalidades"]!.AsArray();
        modalidades.Add(modalidades[0]!.DeepClone());
        modalidades.Add(modalidades[0]!.DeepClone());
        modalidades[1]!["regraRemanejamento"] = "SEGUE_ORDEM_DE_REMANEJAMENTO";
        LeitorEnvelope leitor = new();
        LeituraDasVagas.LerDistribuicao(leitor, documento);
        FieldError causa = leitor.Falha<object>().Errors.Single();
        ConferirCausaNoDocumento.Conferir(documento, causa, "modalidades[1]", "EnvelopeCodec.EnvelopeMalformado",
            "Envelope malformado em 'modalidades': a regra de remanejamento não é a que a natureza da modalidade pede.");
    }

    [Fact]
    public void LerDistribuicao_DuasModalidadesIncoerentes_ParaNaPrimeira()
    {
        JsonObject documento = Documento();
        JsonArray modalidades = documento["modalidades"]!.AsArray();
        modalidades.Add(modalidades[0]!.DeepClone());
        modalidades[0]!["regraRemanejamento"] = "SEGUE_ORDEM_DE_REMANEJAMENTO";
        modalidades[1]!["regraRemanejamento"] = "SEGUE_ORDEM_DE_REMANEJAMENTO";
        LeitorEnvelope leitor = new();
        LeituraDasVagas.LerDistribuicao(leitor, documento);
        FieldError causa = leitor.Falha<object>().Errors.Single();
        causa.Field.Should().Be("modalidades[0]");
        causa.Error.Code.Should().Be("EnvelopeCodec.EnvelopeMalformado");
    }

    private static JsonObject Documento() => JsonNode.Parse("""
        {"distribuicao":[{"ofertaCursoOrigemId":"11111111-1111-1111-1111-111111111111","vagasOfertadasBase":40,
        "percentualDaReservaLegal":null,"regraDistribuicao":{"codigo":"DISTRIBUICAO_INSTITUCIONAL","versao":"v1","hash":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"},
        "regraAjuste":null,"ajusteDoQuadro":null,"referenciaDemografica":null,"grupoAreaEnem":null}],
        "ofertas":[{"ofertaCursoOrigemId":"11111111-1111-1111-1111-111111111111","ofertaCursoCodigo":"VALOR_FICTICIO_X",
        "cursoNome":"Curso fictício","cursoGrau":"Grau","unidadeOfertanteNome":"Unidade","turnos":["MATUTINO"]}],
        "modalidades":[{"ofertaCursoOrigemId":"11111111-1111-1111-1111-111111111111","modalidadeOrigemId":"22222222-2222-2222-2222-222222222222",
        "codigo":"AC","descricao":null,"naturezaLegal":"AMPLA","composicaoVagas":"RESTANTE_DAS_VAGAS_OFERTADAS",
        "composicaoOrigemCodigo":null,"regraRemanejamento":null,"remanejamentoDestino":null,"remanejamentoPar":null,
        "remanejamentoDestinoFinal":null,"regraDeAplicabilidade":null,"acaoQuandoIndeferido":null,"baseLegal":"Norma","quantidadeDeclarada":40}],
        "vagas":[{"ofertaCursoOrigemId":"11111111-1111-1111-1111-111111111111","quadro":[{"modalidadeCodigo":"AC","quantidade":40}],
        "totalDeVagasDoEdital":40,"conferenciaDaReservaLegal":null,"ajusteDoQuadroFixado":null}]}
        """)!.AsObject();
}
