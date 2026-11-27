namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.DocumentoDaConfiguracao;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

/// <summary>
/// As exigências e as regras de comprovação do documento são copiadas com a posição que cada item tinha na lista enviada (nos itens
/// de um grupo, a posição dentro do grupo pai) e cada código como foi escrito; o que o cadastro do tipo de documento dá não entra, e o
/// valor de tipo errado fica ausente, sem exceção.
/// </summary>
public sealed class ExigenciasDoDocumentoLidasTests
{
    private const string BlocoCompleto = """
        {
          "exigencias": [
            { "chave": "identidade", "tipoDocumentoCodigo": "RG", "tipoDocumentoNome": "só leitura", "classificacaoProtecao": "PESSOAL",
              "dadosComprovados": [ "NOME", "DATA_DE_NASCIMENTO" ], "faseChave": "inscricao", "etapaChave": "prova", "formularioCodigo": "FORM_A",
              "aplicabilidade": "CONDICIONAL", "obrigatorio": true, "abrePrazoParaReenvio": true, "consequenciaDoDescumprimento": "RETIRA_A_MODALIDADE",
              "condicoes": [ [ { "codigoDoDado": "SEXO", "operador": "IGUAL", "valor": "F" } ], [ { "codigoDoDado": "UF", "operador": "ALGUM_DE", "valor": [ "PA", "AP" ] } ] ],
              "basesLegais": [ { "referencia": "Lei 1", "abrangencia": "FEDERAL", "situacao": "RESOLVIDO", "observacao": "nota" }, { "referencia": "Edital", "abrangencia": "INTERNA_EDITAL" } ],
              "formatosPermitidos": { "qualquer": false, "lista": [ { "formato": "PDF", "tamanhoMaximoBytesMax": 2048 }, { "formato": "PNG" } ] },
              "tamanhoMaximoBytes": 4096, "modeloId": "0197a6c0-0000-7000-8000-000000000001",
              "prazoDeEmissao": { "escolha": "COM_PRAZO", "valor": 90, "unidade": "DIAS", "efeitoForaDoPrazo": "RECUSAR_DOCUMENTO",
                                  "referencia": { "tipo": "INICIO_DA_FASE", "faseChave": "inscricao" } },
              "validade": { "escolha": "EXIGE_DOCUMENTO_VALIDO", "efeitoDoVencimento": "ACEITAR_COM_AVISO",
                            "referencia": { "tipo": "DATA_ESPECIFICA", "data": "2027-03-15" } } },
            { "chave": "renda" }
          ],
          "regrasDeComprovacao": [
            { "forma": "TODOS_OS_ITENS", "abrePrazoParaReenvio": true, "consequenciaDoDescumprimento": "ELIMINA_DO_PROCESSO",
              "basesLegais": [ { "referencia": "Portaria" } ],
              "itens": [
                { "forma": "DOCUMENTO", "exigenciaChave": "renda", "quantidadeMinima": 3, "criterioDeDistincao": "COMPETENCIA_MENSAL",
                  "dataReferencia": "2027-01-31", "ocorrenciasEsperadas": [ "2026-11", "2026-12" ], "grupoDoFormularioParaRepeticao": "RENDAS" },
                { "forma": "QUANTIDADE_MINIMA_DE_ITENS", "quantidadeMinima": 1, "itens": [ { "forma": "DOCUMENTO", "exigenciaChave": "identidade" } ] }
              ] },
            { "forma": "DOCUMENTO", "exigenciaChave": "identidade" }
          ]
        }
        """;

    private static ExigenciasNoDocumento Lidas(string bloco) => ExigenciasDoDocumentoLidas.De(JsonNode.Parse(bloco))!;

    [Fact(DisplayName = "O bloco ausente não declara exigência, e o bloco vazio declara listas vazias")]
    public void BlocoAusente_EhNulo()
    {
        ExigenciasDoDocumentoLidas.De(null).Should().BeNull();
        ExigenciasDoDocumentoLidas.De(JsonNode.Parse("[]")).Should().BeNull();
        Lidas("{}").Exigencias.Should().BeEmpty();
        Lidas("{}").RegrasDeComprovacao.Should().BeEmpty();
    }

    [Fact(DisplayName = "A exigência guarda a posição, a chave do autor e os códigos como escritos, sem o que é só de leitura")]
    public void Exigencia_TemAPosicaoEAChaveDoAutor()
    {
        ExigenciaNoDocumento identidade = Lidas(BlocoCompleto).Exigencias[0];

        Lidas(BlocoCompleto).Exigencias.Select(static e => (e.PosicaoNoDocumento, e.Chave)).Should().Equal((0, "identidade"), (1, "renda"));
        identidade.TipoDocumentoCodigo.Should().Be("RG");
        identidade.FaseChave.Should().Be("inscricao");
        identidade.EtapaChave.Should().Be("prova");
        identidade.FormularioCodigo.Should().Be("FORM_A");
        identidade.Aplicabilidade.Should().Be(Aplicabilidade.Condicional);
        identidade.Obrigatorio.Should().BeTrue();
        identidade.AbrePrazoParaReenvio.Should().BeTrue();
        identidade.ConsequenciaDoDescumprimento.Should().Be(ConsequenciaDoDescumprimento.RetiraAModalidade);
        identidade.DadosComprovados.Should().Equal("NOME", "DATA_DE_NASCIMENTO");
    }

    [Fact(DisplayName = "As condições guardam cada alternativa como lista de condições com o dado, o operador e o valor como escritos")]
    public void Condicoes_SaoAlternativasDeCondicoes()
    {
        ExigenciaNoDocumento identidade = Lidas(BlocoCompleto).Exigencias[0];

        identidade.Condicoes.Should().HaveCount(2);
        identidade.Condicoes[0].Should().ContainSingle().Which.Should().Match<CondicaoSobreDadoNoDocumento>(
            c => c.CodigoDoDado == "SEXO" && c.Operador == "IGUAL" && c.Valor!.Value.GetString() == "F");
        identidade.Condicoes[1][0].Valor!.Value.EnumerateArray().Select(static v => v.GetString()).Should().Equal("PA", "AP");
    }

    [Fact(DisplayName = "O valor nulo escrito na condição é guardado como valor presente, e o valor ausente fica ausente")]
    public void Condicoes_ValorNuloEscritoNaoSeConfundeComValorAusente()
    {
        ExigenciaNoDocumento exigencia = Lidas("""
            { "exigencias": [ { "chave": "renda", "condicoes": [ [
                { "codigoDoDado": "SEXO", "operador": "IGUAL", "valor": null },
                { "codigoDoDado": "UF", "operador": "IGUAL" } ] ] } ] }
            """).Exigencias[0];

        CondicaoSobreDadoNoDocumento[] condicoes = [.. exigencia.Condicoes.Single()];
        condicoes[0].Valor.Should().NotBeNull();
        condicoes[0].Valor!.Value.ValueKind.Should().Be(JsonValueKind.Null);
        condicoes[1].Valor.Should().BeNull();
    }

    [Fact(DisplayName = "As bases legais, os formatos, o prazo e a validade guardam a posição e o que foi escrito")]
    public void Requisitos_SaoCopiadosComoEscritos()
    {
        ExigenciaNoDocumento identidade = Lidas(BlocoCompleto).Exigencias[0];

        identidade.BasesLegais.Should().Equal(
            new BaseLegalNoDocumento(0, "Lei 1", TipoAbrangencia.Federal, StatusBaseLegal.Resolvido, "nota"),
            new BaseLegalNoDocumento(1, "Edital", TipoAbrangencia.InternaEdital, null, null));
        identidade.FormatosPermitidos!.Qualquer.Should().BeFalse();
        identidade.FormatosPermitidos.Lista.Should().Equal(
            new FormatoPermitidoNoDocumento(0, FormatoPermitido.Pdf, 2048), new FormatoPermitidoNoDocumento(1, FormatoPermitido.Png, null));
        identidade.TamanhoMaximoBytes.Should().Be(4096);
        identidade.ModeloId.Should().Be(Guid.Parse("0197a6c0-0000-7000-8000-000000000001"));
        identidade.PrazoDeEmissao.Should().Be(new PrazoDeEmissaoNoDocumento(
            EscolhaDoPrazoDeEmissao.ComPrazo, 90, UnidadeDoPrazoDeEmissao.Dias,
            new(TipoDaDataDeReferencia.InicioDaFase, null, "inscricao"), EfeitoNaAnaliseDoDocumento.RecusarDocumento));
        identidade.Validade.Should().Be(new ValidadeNoDocumento(
            EscolhaDaValidade.ExigeDocumentoValido, new(TipoDaDataDeReferencia.DataEspecifica, new DateOnly(2027, 3, 15), null), EfeitoNaAnaliseDoDocumento.AceitarComAviso));
    }

    [Fact(DisplayName = "A exigência que traz só a chave deixa o resto ausente, vazio ou no valor de partida")]
    public void ExigenciaMinima_DeixaORestoAusente()
    {
        ExigenciaNoDocumento renda = Lidas(BlocoCompleto).Exigencias[1];

        renda.TipoDocumentoCodigo.Should().BeNull();
        renda.Aplicabilidade.Should().BeNull();
        renda.Obrigatorio.Should().BeNull();
        renda.AbrePrazoParaReenvio.Should().BeFalse();
        renda.Condicoes.Should().BeEmpty();
        renda.BasesLegais.Should().BeEmpty();
        renda.FormatosPermitidos.Should().BeNull();
        renda.PrazoDeEmissao.Should().BeNull();
        renda.Validade.Should().BeNull();
        renda.DadosComprovados.Should().BeEmpty();
    }

    [Fact(DisplayName = "A regra de comprovação guarda a posição na lista, e cada item de grupo a posição dentro do grupo pai")]
    public void Regras_TemAPosicaoNaListaENoGrupo()
    {
        IReadOnlyList<RegraDeComprovacaoNoDocumento> regras = Lidas(BlocoCompleto).RegrasDeComprovacao;

        regras.Select(static r => (r.PosicaoNoDocumento, r.Forma, r.ExigenciaChave)).Should().Equal(
            (0, FormaDeComprovacao.TodosOsItens, null), (1, FormaDeComprovacao.Documento, "identidade"));
        regras[0].Itens.Select(static r => (r.PosicaoNoDocumento, r.Forma)).Should().Equal(
            (0, FormaDeComprovacao.Documento), (1, FormaDeComprovacao.QuantidadeMinimaDeItens));
        regras[0].Itens[1].Itens.Should().ContainSingle().Which.PosicaoNoDocumento.Should().Be(0);
        regras[0].Itens[1].Itens[0].ExigenciaChave.Should().Be("identidade");
    }

    [Fact(DisplayName = "A regra de comprovação guarda a quantidade, a consequência, as bases legais e a distinção das apresentações como escritas")]
    public void Regra_TemOsCamposComoEscritos()
    {
        RegraDeComprovacaoNoDocumento grupo = Lidas(BlocoCompleto).RegrasDeComprovacao[0];
        RegraDeComprovacaoNoDocumento renda = grupo.Itens[0];

        grupo.AbrePrazoParaReenvio.Should().BeTrue();
        grupo.ConsequenciaDoDescumprimento.Should().Be(ConsequenciaDoDescumprimento.EliminaDoProcesso);
        grupo.BasesLegais.Should().ContainSingle().Which.Referencia.Should().Be("Portaria");
        renda.QuantidadeMinima.Should().Be(3);
        renda.CriterioDeDistincao.Should().Be(CriterioDeDistincaoDasApresentacoes.CompetenciaMensal);
        renda.DataReferencia.Should().Be(new DateOnly(2027, 1, 31));
        renda.OcorrenciasEsperadas.Should().Equal("2026-11", "2026-12");
        renda.GrupoDoFormularioParaRepeticao.Should().Be("RENDAS");
    }

    [Fact(DisplayName = "O valor de tipo errado fica ausente ou no valor de partida, sem exceção")]
    public void TipoErrado_FicaAusente()
    {
        ExigenciasNoDocumento bloco = Lidas("""
            { "exigencias": [ { "chave": "x", "tipoDocumentoCodigo": 5, "obrigatorio": "sim", "tamanhoMaximoBytes": "grande", "modeloId": "não é identificador",
                                "aplicabilidade": "DESCONHECIDA", "condicoes": [ "texto", [ 3, { "codigoDoDado": 1 } ] ], "dadosComprovados": [ 1, "NOME" ],
                                "validade": { "escolha": 2, "referencia": { "tipo": "DATA_ESPECIFICA", "data": "ontem" } } } ],
              "regrasDeComprovacao": [ { "forma": 1, "quantidadeMinima": "duas", "dataReferencia": "31/01/2027", "itens": "nenhum" } ] }
            """);

        ExigenciaNoDocumento exigencia = bloco.Exigencias.Should().ContainSingle().Subject;
        exigencia.TipoDocumentoCodigo.Should().BeNull();
        exigencia.Obrigatorio.Should().BeNull();
        exigencia.TamanhoMaximoBytes.Should().BeNull();
        exigencia.ModeloId.Should().BeNull();
        exigencia.Aplicabilidade.Should().BeNull();
        exigencia.Condicoes.Should().HaveCount(1);
        exigencia.Condicoes[0].Should().ContainSingle().Which.CodigoDoDado.Should().BeNull();
        exigencia.DadosComprovados.Should().Equal(string.Empty, "NOME");
        exigencia.Validade!.Escolha.Should().BeNull();
        exigencia.Validade.Referencia!.Data.Should().BeNull();
        RegraDeComprovacaoNoDocumento regra = bloco.RegrasDeComprovacao.Should().ContainSingle().Subject;
        regra.Forma.Should().BeNull();
        regra.QuantidadeMinima.Should().BeNull();
        regra.DataReferencia.Should().BeNull();
        regra.Itens.Should().BeEmpty();
    }
}
