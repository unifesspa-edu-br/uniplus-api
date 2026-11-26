namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

public sealed class ContextoDaColetaNoEnvelopeTests
{
    [Fact]
    public void Canonicalizar_FormulariosETermos_EscreveRotulosDeclarados()
    {
        JsonArray formularios = Envelope()["formularios"]!.AsArray();
        formularios.Should().NotBeEmpty();
        foreach (JsonNode? formulario in formularios)
        {
            Conferir<FinalidadeFormulario>(formulario!, "finalidade");
            foreach (JsonNode? termo in formulario!["termos"]!.AsArray())
            {
                Conferir<FormaAceite>(termo!, "formaAceite");
            }
        }
    }

    [Fact]
    public void Canonicalizar_DadosColetados_EscreveRotulosDeclarados()
    {
        JsonArray dados = Envelope()["dadosDosFormularios"]!.AsArray();
        dados.Should().NotBeEmpty();
        foreach (JsonNode? dado in dados)
        {
            Conferir<FinalidadeFormulario>(dado!, "finalidade");
            Conferir<TipoDeCampo>(dado!, "tipoDeCampo");
            Conferir<OrigemValoresColeta>(dado!, "origemValores");
        }
    }

    [Fact]
    public void Canonicalizar_GruposColetados_EscreveRotuloDeclarado()
    {
        JsonArray grupos = Envelope()["gruposDosFormularios"]!.AsArray();
        grupos.Should().NotBeEmpty();
        foreach (JsonNode? grupo in grupos)
        {
            Conferir<FinalidadeFormulario>(grupo!, "finalidade");
        }
    }

    [Fact]
    public void Canonicalizar_Dependencias_EscreveRotuloDeclarado()
    {
        JsonArray ligacoes = Envelope()["dependenciasEntreItens"]!["ligacoes"]!.AsArray();
        ligacoes.Should().NotBeEmpty();
        foreach (JsonNode? ligacao in ligacoes)
        {
            Conferir<MotivoDaDependencia>(ligacao!, "tipo");
        }
    }

    [Fact]
    public void Canonicalizar_Exigencias_EscreveRotulosDeclarados()
    {
        JsonArray exigencias = Envelope()["documentosExigidos"]!["exigencias"]!.AsArray();
        exigencias.Should().NotBeEmpty();
        foreach (JsonNode? exigencia in exigencias)
        {
            Conferir<FinalidadeFormulario>(exigencia!, "finalidade");
            Conferir<Aplicabilidade>(exigencia!, "aplicabilidade");
        }
    }

    [Fact]
    public void Canonicalizar_RegrasDeComprovacao_EscreveRotuloDeclarado()
    {
        JsonArray regras = Envelope()["regrasDeComprovacao"]!.AsArray();
        regras.Should().NotBeEmpty();
        foreach (JsonNode? regra in regras)
        {
            Conferir<FormaDeComprovacao>(regra!, "forma");
        }
    }

    [Fact]
    public void Canonicalizar_RegraSemDistincao_NaoEscreveRotuloDaDistincao()
    {
        JsonNode regra = Envelope()["regrasDeComprovacao"]!.AsArray().First(static item => item!["criterioDeDistincao"] is null)!;

        regra["criterioDeDistincao"].Should().BeNull();
        regra["contexto"]!.AsObject().ContainsKey("criterioDeDistincaoRotulo").Should().BeFalse();
    }

    [Fact]
    public void Canonicalizar_ReferenciaTemporal_EscreveRotuloDeclarado()
    {
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico();
        ReferenciaTemporalFatos referencia = ReferenciaTemporalFatos.Criar(ReferenciaTipo.DataEspecifica, new DateOnly(2026, 1, 15), null).Value!;
        processo.DefinirReferenciaTemporalFatos(referencia).IsSuccess.Should().BeTrue();

        JsonNode envelope = JsonNode.Parse(ExemploDeEnvelope.Canonicalizador.Canonicalizar(ExemploDeEnvelopeEntrada.Entrada(processo)).Bytes)!;

        Conferir<ReferenciaTipo>(envelope["documentosExigidos"]!["referenciaTemporalDosDados"]!, "tipo");
    }

    [Fact]
    public void Canonicalizar_BaseLegalDaExigencia_EscreveRotulosDeclarados()
    {
        JsonNode baseLegal = ExigenciaDeReferencia()["basesLegais"]![0]!;
        Conferir<TipoAbrangencia>(baseLegal, "abrangencia");
        Conferir<StatusBaseLegal>(baseLegal, "situacao");
    }

    [Fact]
    public void Canonicalizar_PrazoEValidade_EscreveRotulosDeclarados()
    {
        JsonNode exigencia = ExigenciaDeReferencia();
        JsonNode prazo = exigencia["prazoDeEmissao"]!;
        JsonNode validade = exigencia["validade"]!;
        Conferir<EscolhaDoPrazoDeEmissao>(prazo, "escolha");
        Conferir<EscolhaDaValidade>(validade, "escolha");
    }

    [Fact]
    public void Canonicalizar_FormatosPermitidos_EscreveRotulosDeclarados()
    {
        JsonArray formatos = ExigenciaDeReferencia()["formatosPermitidos"]!["lista"]!.AsArray();
        formatos.Should().NotBeEmpty();
        foreach (JsonNode? formato in formatos)
        {
            Conferir<FormatoPermitido>(formato!, "formato");
        }
    }

    [Fact]
    public void Canonicalizar_ModeloDaExigencia_EscreveRotuloDeclarado()
    {
        Conferir<FormatoDeDocumento>(ExigenciaDeReferencia()["modelo"]!, "formato");
    }

    [Fact]
    public void Canonicalizar_ObrigatoriedadeLegal_EscreveRotuloDeclarado()
    {
        RegraAvaliada regra = RegraDeObrigatoriedade();
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico();
        EntradaCanonicalizacao entrada = ExemploDeEnvelopeEntrada.Entrada(processo, conformidade: new ResultadoConformidade([regra], []));
        JsonNode envelope = JsonNode.Parse(ExemploDeEnvelope.Canonicalizador.Canonicalizar(entrada).Bytes)!;

        Conferir<CategoriaObrigatoriedade>(envelope["documentosExigidos"]!["obrigatoriedades"]![0]!, "categoria");
    }

    [Fact]
    public void SerializarRegrasDeComprovacao_BaseLegal_EscreveRotulosDeclarados()
    {
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico();
        RegraDeComprovacao original = ExemploDeEnvelopeDocumentos.RegrasDeComprovacaoRicas(0, false)[1];
        BaseLegalDaComprovacao baseLegal = BaseLegalDaComprovacao.Criar("Norma institucional", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, null).Value!;
        RegraDeComprovacao grupo = RegraDeComprovacao.ReidratarGrupo(original.Id, 0,
            new CombinacaoDoGrupo(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, null), [baseLegal], [.. original.Itens], null);
        processo.DefinirDocumentosExigidos([grupo]).IsSuccess.Should().BeTrue();

        JsonNode objeto = SerializacaoDasExigencias.SerializarRegrasDeComprovacao(processo)[0]!["basesLegais"]![0]!;

        Conferir<TipoAbrangencia>(objeto, "abrangencia");
        Conferir<StatusBaseLegal>(objeto, "situacao");
    }

    [Fact]
    public void SerializarRegrasDeComprovacao_Consequencia_EscreveRotuloDeclarado()
    {
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico();
        RegraDeComprovacao original = ExemploDeEnvelopeDocumentos.RegrasDeComprovacaoRicas(0, false)[1];
        BaseLegalDaComprovacao baseLegal = BaseLegalDaComprovacao.Criar("Norma institucional", TipoAbrangencia.InternaNorma, StatusBaseLegal.Resolvido, null).Value!;
        RegraDeComprovacao grupo = RegraDeComprovacao.ReidratarGrupo(original.Id, 0,
            new CombinacaoDoGrupo(FormaDeComprovacao.QuantidadeMinimaDeItens, 1, "ELIMINA_DO_PROCESSO"), [baseLegal], [.. original.Itens], null);
        processo.DefinirDocumentosExigidos([grupo]).IsSuccess.Should().BeTrue();

        Conferir<ConsequenciaDoDescumprimento>(SerializacaoDasExigencias.SerializarRegrasDeComprovacao(processo)[0]!, "consequenciaDoDescumprimento");
    }

    [Fact]
    public void SerializarRegrasDeComprovacao_Distincao_EscreveRotuloDeclarado()
    {
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico();
        DocumentoExigido documento = ExemploDeEnvelopeDocumentos.RegrasDeComprovacaoRicas(0, false)[0].DocumentoExigido!;
        Result<RegraDeComprovacao> resultado = RegraDeComprovacao.CriarRegraDeDocumento(documento, 0, quantidadeMinima: 3,
            criterioDeDistincao: CriterioDeDistincaoDasApresentacoes.CompetenciaMensal, dataReferencia: new DateOnly(2026, 3, 31));
        resultado.IsSuccess.Should().BeTrue();
        processo.DefinirDocumentosExigidos([resultado.Value!]).IsSuccess.Should().BeTrue();

        Conferir<CriterioDeDistincaoDasApresentacoes>(SerializacaoDasExigencias.SerializarRegrasDeComprovacao(processo)[0]!, "criterioDeDistincao");
    }

    private static RegraAvaliada RegraDeObrigatoriedade() => new(
        Guid.Parse("0199a000-0000-7000-8000-00000000a001"), "REGRA", CategoriaObrigatoriedade.Outros,
        "SISU", new Customizado(JsonSerializer.SerializeToElement(new { limite = 1 })), true, null,
        "Lei de teste", null, null, "Descrição", new DateOnly(2026, 1, 1), null, new string('a', 64));

    private static JsonNode ExigenciaDeReferencia() =>
        JsonNode.Parse(EnvelopeDeReferencia.CanonicalizarReferencia().Bytes)!["documentosExigidos"]!["exigencias"]![0]!;

    private static JsonNode Envelope() => JsonNode.Parse(ExemploDeEnvelope.Canonicalizador.Canonicalizar(
        ExemploDeEnvelopeEntrada.Entrada(ExemploDeEnvelopeProcessoRico.ProcessoRico(comRegrasDeComprovacao: true))).Bytes)!;

    private static void Conferir<TEnum>(JsonNode objeto, string campo)
        where TEnum : struct, Enum
    {
        TEnum valor = CatalogoDe<TEnum>.Obter(objeto[campo]!.GetValue<string>());
        objeto["contexto"]![$"{campo}Rotulo"]!.GetValue<string>().Should().Be(valor.Rotulo());
    }
}
