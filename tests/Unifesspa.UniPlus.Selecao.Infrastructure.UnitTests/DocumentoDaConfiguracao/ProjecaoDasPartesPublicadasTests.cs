namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Json.Schema;

using Unifesspa.UniPlus.Selecao.Application.Services.ExportacaoDaVersaoPublicada;
using Unifesspa.UniPlus.Selecao.Infrastructure.DocumentoDaConfiguracao;

public sealed class ProjecaoDasPartesPublicadasTests
{
    private static readonly EsquemaDoDocumentoDaConfiguracao Esquema = new();

    [Theory]
    [InlineData("opcoesDeCurso", """{"opcoesDeCurso":{"numero":1}}""", """{"numero":1}""")]
    [InlineData("atendimento", """{"atendimento":{"condicoes":[],"recursos":[],"tiposDeficiencia":[]}}""", """{"condicoes":[],"recursos":[],"tiposDeficiencia":[]}""")]
    [InlineData("fada", """{"fada":{"presente":false}}""", """{"aplica":false}""")]
    [InlineData("taxaDeInscricao", """{"taxaInscricao":{"presente":true,"cobra":false,"valor":null,"fundamentos":[]}}""", """{"cobra":false,"fundamentos":[]}""")]
    [InlineData("divulgacao", """{"divulgacao":{"camposPublicos":["NUMERO_INSCRICAO"],"regraNomeAbreviado":null,"justificativa":null}}""", """{"camposPublicos":["NUMERO_INSCRICAO"]}""")]
    [InlineData("provaPresencial", """{"provaPresencial":{"temProvaPresencial":false,"municipios":[]}}""", """{"temProvaPresencial":false,"municipios":[]}""")]
    [InlineData("tiposDeAtoCongelados", """{"tiposDeAto":[{"codigo":"INSCRICAO","unicoPorObjeto":false,"ehResultado":true,"congelaConfiguracao":false}]}""", """[{"codigo":"INSCRICAO","unicoPorObjeto":false,"ehResultado":true,"congelaConfiguracao":false}]""")]
    [InlineData("coleta", """{"calculosDosDados":[{"codigoDoDado":"REGIME","regras":[{"ordem":0,"contribui":"AC","quando":null}]}]}""", """{"calculosDosDados":[{"codigoDoDado":"REGIME","regras":[{"ordem":0,"contribui":"AC","quando":null}]}]}""")]
    [InlineData("identificacao", """{"tipoProcesso":{"origemId":"11111111-1111-1111-1111-111111111111","codigo":"SISU","nome":"Seleção"},"identidadesUnidade":{"administradora":{"origemId":"11111111-1111-1111-1111-111111111111","codigo":"CEPS","nome":"Centro","sigla":"CEPS","slug":"ceps","tipo":"ADMINISTRATIVA","cidadeCodigoIbge":null,"cidadeNome":null,"cidadeUf":null}},"localidade":{"codigoIbge":"1504208","nome":"Marabá","uf":"PA","fusoHorario":"America/Belem"},"identificadorLegivel":"certame-2026","algoritmoContagemPrazo":{"presente":false}}""", """{"tipoProcessoCodigo":"SISU","tipoProcessoNome":"Seleção","unidadeAdministradoraCodigo":"CEPS","unidadeAdministradoraNome":"Centro","unidadeAdministradoraSigla":"CEPS","localidadeCodigoIbge":"1504208","localidadeNome":"Marabá","localidadeUf":"PA","identificadorLegivel":"certame-2026"}""")]
    [InlineData("cascataRemanejamento", """{"cascataRemanejamento":{"presente":true,"regra":{"codigo":"CASCATA","versao":"v1","hash":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"},"destinoFinalCodigo":"AC","ordens":[{"origem":"L1","destinos":["L2","AC"]}]}}""", """{"regra":{"codigo":"CASCATA","versao":"v1"},"destinoFinalCodigo":"AC","destinos":[{"modalidadeOrigemCodigo":"L1","ordem":1,"modalidadeDestinoCodigo":"L2"},{"modalidadeOrigemCodigo":"L1","ordem":2,"modalidadeDestinoCodigo":"AC"}]}""")]
    [InlineData("classificacao", """{"classificacao":{"regraCalculo":{"codigo":"CLASSIFICACAO-IMPORTADA","versao":"v1","hash":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"},"regraArredondamento":null,"casasArredondamento":null,"escalaNotaFinal":null,"regraOrdemAlocacao":{"codigo":"ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA","versao":"v1","hash":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"},"baseadoEmEnem":false,"regrasEliminacao":[],"resolucaoPesoAreaEnem":null,"quadroPesoAreaEnem":[]},"criteriosDesempate":[{"ordem":1,"regra":{"codigo":"DESEMPATE-MAIOR-IDADE","versao":"v1","hash":"cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"},"parametros":{}}]}""", """{"regraCalculo":{"codigo":"CLASSIFICACAO-IMPORTADA","versao":"v1"},"regraOrdemAlocacao":{"codigo":"ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA","versao":"v1"},"baseadoEmEnem":false,"regrasEliminacao":[],"criteriosDeDesempate":[{"ordem":1,"regra":{"codigo":"DESEMPATE-MAIOR-IDADE","versao":"v1"},"parametros":{}}],"quadroPesoAreaEnem":[]}""")]
    [InlineData("distribuicaoVagas", """{"distribuicao":[{"ofertaCursoOrigemId":"11111111-1111-1111-1111-111111111111","vagasOfertadasBase":40,"percentualDaReservaLegal":null,"regraDistribuicao":{"codigo":"DISTRIBUICAO-INSTITUCIONAL","versao":"v1","hash":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"},"regraAjuste":null,"ajusteDoQuadro":null,"referenciaDemografica":null,"grupoAreaEnem":null}],"ofertas":[{"ofertaCursoOrigemId":"11111111-1111-1111-1111-111111111111","ofertaCursoCodigo":"MEDICINA","cursoNome":"Medicina","cursoGrau":"Bacharelado","unidadeOfertanteNome":"Campus","turnos":["MATUTINO"]}],"modalidades":[{"ofertaCursoOrigemId":"11111111-1111-1111-1111-111111111111","modalidadeOrigemId":"22222222-2222-2222-2222-222222222222","codigo":"AC","descricao":null,"naturezaLegal":"AMPLA","composicaoVagas":"RESTANTE_DAS_VAGAS_OFERTADAS","composicaoOrigemCodigo":null,"regraRemanejamento":null,"remanejamentoDestino":null,"remanejamentoPar":null,"remanejamentoDestinoFinal":null,"regraDeAplicabilidade":null,"acaoQuandoIndeferido":null,"baseLegal":"Norma","quantidadeDeclarada":40}],"vagas":[{"ofertaCursoOrigemId":"11111111-1111-1111-1111-111111111111","quadro":[{"modalidadeCodigo":"AC","quantidade":40}],"totalDeVagasDoEdital":40,"conferenciaDaReservaLegal":null,"ajusteDoQuadroFixado":null}]}""", """[{"ofertaCursoCodigo":"MEDICINA","vagasOfertadasBase":40,"percentualDaReservaLegal":null,"regraDistribuicao":{"codigo":"DISTRIBUICAO-INSTITUCIONAL","versao":"v1"},"regraAjuste":null,"ajusteDoQuadro":null,"referenciaDemograficaCodigo":null,"modalidades":[{"codigo":"AC","quantidadeDeclarada":40,"descricao":null}],"cursoNome":"Medicina","cursoGrau":"Bacharelado","unidadeOfertanteNome":"Campus","turnos":["MATUTINO"],"quadro":[{"modalidadeCodigo":"AC","quantidade":40}],"totalDeVagasDoEdital":40}]""")]
    [InlineData("atendimento", """{"atendimento":{"condicoes":[{"condicaoOrigemId":"11111111-1111-1111-1111-111111111111","condicaoCodigo":"GESTANTE","condicaoNome":"Gestante"}],"recursos":[],"tiposDeficiencia":[]}}""", """{"condicoes":[{"condicaoCodigo":"GESTANTE","condicaoNome":"Gestante"}],"recursos":[],"tiposDeficiencia":[]}""")]
    [InlineData("fada", """{"fada":{"presente":true,"regra":{"codigo":"FADA-MULTIPLICATIVO","versao":"v1","hash":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"},"fator":"1.2000","teto":null,"codigoDoDadoDeElegibilidade":"ELEGIVEL","baseLegalId":"11111111-1111-1111-1111-111111111111","baseLegalCodigo":"NORMA","tipoInstrumento":"RESOLUCAO","identificacao":"123","descricao":"Norma","municipios":[{"codigoIbge":"1504208","nome":"Marabá","uf":"PA"}]}}""", """{"aplica":true,"regra":{"codigo":"FADA-MULTIPLICATIVO","versao":"v1"},"fator":1.2000,"codigoDoDadoDeElegibilidade":"ELEGIVEL","baseLegalCodigo":"NORMA","tipoInstrumento":"RESOLUCAO","identificacao":"123","descricao":"Norma","municipios":[{"codigoIbge":"1504208","nome":"Marabá","uf":"PA"}]}""")]
    [InlineData("coleta", """{"calculosDosDados":[{"codigoDoDado":"REGIME","regras":[{"ordem":0,"contribui":"AC","quando":null},{"ordem":1,"contribui":"L1","quando":[[{"codigoDoDado":"RENDA","operador":"IGUAL","valor":"BAIXA"}]]}]}]}""", """{"calculosDosDados":[{"codigoDoDado":"REGIME","regras":[{"ordem":0,"contribui":"AC","quando":null},{"ordem":1,"contribui":"L1","quando":[[{"codigoDoDado":"RENDA","operador":"IGUAL","valor":"BAIXA"}]]}]}]}""")]
    [InlineData("classificacao", """{"classificacao":{"regraCalculo":{"codigo":"CLASSIFICACAO-IMPORTADA","versao":"v1","hash":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"},"regraArredondamento":null,"casasArredondamento":null,"escalaNotaFinal":null,"regraOrdemAlocacao":{"codigo":"ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA","versao":"v1","hash":"bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"},"baseadoEmEnem":true,"regrasEliminacao":[],"resolucaoPesoAreaEnem":"NORMA","quadroPesoAreaEnem":[{"grupoAreaEnem":{"codigo":"SAUDE","rotulo":"Saúde"},"baseLegal":"Norma","areas":[{"codigo":"CN","rotulo":"Natureza","peso":"2.0000","corte":"100.0000"}]}]},"criteriosDesempate":[]}""", """{"regraCalculo":{"codigo":"CLASSIFICACAO-IMPORTADA","versao":"v1"},"regraOrdemAlocacao":{"codigo":"ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA","versao":"v1"},"baseadoEmEnem":true,"regrasEliminacao":[],"criteriosDeDesempate":[],"resolucaoPesoAreaEnem":"NORMA","quadroPesoAreaEnem":[{"grupoAreaEnemCodigo":"SAUDE","grupoAreaEnemRotulo":"Saúde","baseLegal":"Norma","areas":[{"codigo":"CN","rotulo":"Natureza","peso":2.0000,"corte":100.0000}]}]}""")]
    [InlineData("provaPresencial", """{"provaPresencial":{"temProvaPresencial":true,"municipios":[{"codigoIbge":"1504208","uf":"PA","nome":"Marabá"},{"codigoIbge":"1501402","uf":"PA","nome":"Belém"}]}}""", """{"temProvaPresencial":true,"municipios":[{"codigoIbge":"1504208","nome":"Marabá","uf":"PA"},{"codigoIbge":"1501402","nome":"Belém","uf":"PA"}]}""")]
    [InlineData("identificacao", """{"algoritmoContagemPrazo":{"presente":true,"codigo":"CONTAGEM","versao":"v1","hash":"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"}}""", """{"algoritmoContagemPrazo":{"codigo":"CONTAGEM","versao":"v1"}}""")]
    [InlineData("identificacao", """{"nome":"Seleção pública"}""", """{"nome":"Seleção pública"}""")]
    // As etapas de congelamento acrescentarão chave ao lado do identificador de cada objeto.
    [InlineData("cronograma", """{"cronogramaFases":{"fases":[{"id":"11111111-1111-1111-1111-111111111111","chave":"fase","tipoFaseOrigemId":"33333333-3333-3333-3333-333333333333","codigo":"INSCRICAO","tipoFaseNome":"Inscrição","ordem":1,"unidadeResponsavel":{"origemId":"77777777-7777-7777-7777-777777777777","codigo":"CEPS","nome":"Centro","sigla":"C"},"bancasRequeridas":[],"regraRecurso":null}]}}""", """{"fases":[{"id":"11111111-1111-1111-1111-111111111111","chave":"fase","tipoFaseCodigo":"INSCRICAO","tipoFaseNome":"Inscrição","unidadeResponsavelCodigo":"CEPS","unidadeResponsavelNome":"Centro","unidadeResponsavelSigla":"C","bancasRequeridas":[]}]}""")]
    [InlineData("etapas", """{"cronogramaFases":{"fases":[{"id":"11111111-1111-1111-1111-111111111111","chave":"fase","codigo":"INSCRICAO"}]},"etapas":[{"id":"44444444-4444-4444-4444-444444444444","chave":"etapa","faseCodigo":"INSCRICAO","tipoEtapa":{"codigo":"PROVA"},"itensDeAvaliacao":[{"id":"55555555-5555-5555-5555-555555555555","chave":"item","rotulo":"Questão","pontuacaoMaxima":"10.00"}],"bancas":[],"recursos":[]}]}""", """[{"id":"44444444-4444-4444-4444-444444444444","chave":"etapa","tipoEtapaCodigo":"PROVA","itensDeAvaliacao":[{"id":"55555555-5555-5555-5555-555555555555","chave":"item","rotulo":"Questão","pontuacaoMaxima":10.00}],"faseChave":"fase","bancas":[],"recursos":[]}]""")]
    [InlineData("etapas", """{"etapas":[{"chave":"etapa","itensDeAvaliacao":[{"id":"22222222-2222-2222-2222-222222222222","chave":"item_b","ordem":2,"rotulo":"Segundo","pontuacaoMaxima":"6.0000"},{"id":"11111111-1111-1111-1111-111111111111","chave":"item_a","ordem":1,"rotulo":"Primeiro","pontuacaoMaxima":"4.0000"}],"escalaDeAtendimento":[{"chave":"completo","ordem":3,"rotulo":"Completo","fracao":"1.0000"},{"chave":"ausente","ordem":1,"rotulo":"Ausente","fracao":"0.0000"},{"chave":"parcial","ordem":2,"rotulo":"Parcial","fracao":"0.5000"}],"bancas":[],"recursos":[]}]}""", """[{"chave":"etapa","itensDeAvaliacao":[{"id":"11111111-1111-1111-1111-111111111111","chave":"item_a","rotulo":"Primeiro","pontuacaoMaxima":4.0000},{"id":"22222222-2222-2222-2222-222222222222","chave":"item_b","rotulo":"Segundo","pontuacaoMaxima":6.0000}],"escalaDeAtendimento":[{"chave":"ausente","rotulo":"Ausente","fracao":0.0000},{"chave":"parcial","rotulo":"Parcial","fracao":0.5000},{"chave":"completo","rotulo":"Completo","fracao":1.0000}],"bancas":[],"recursos":[]}]""")]
    [InlineData("etapas", """{"etapas":[{"chave":"etapa","escalaDeAtendimento":null}]}""", """[{"chave":"etapa"}]""")]
    [InlineData("documentosExigidos", """{"cronogramaFases":{"fases":[{"id":"11111111-1111-1111-1111-111111111111","chave":"fase"}]},"documentosExigidos":{"exigencias":[{"exigenciaId":"66666666-6666-6666-6666-666666666666","chave":"documento","exigidoNaFaseId":"11111111-1111-1111-1111-111111111111"}]},"regrasDeComprovacao":[{"id":"88888888-8888-8888-8888-888888888888","ordem":1,"forma":"DOCUMENTO","exigenciaId":"66666666-6666-6666-6666-666666666666","itens":[]}]}""", """{"exigencias":[{"chave":"documento","faseChave":"fase"}],"regrasDeComprovacao":[{"forma":"DOCUMENTO","exigenciaChave":"documento","itens":[]}]}""")]
    [InlineData("documentosExigidos", """{"cronogramaFases":{"fases":[{"id":"11111111-1111-1111-1111-111111111111","chave":"fase"}]},"documentosExigidos":{"exigencias":[{"exigenciaId":"66666666-6666-6666-6666-666666666666","chave":"documento","prazoDeEmissao":{"escolha":"comPrazo","valor":90,"unidade":"dias","referencia":{"tipo":"inicioDaFase","faseId":"11111111-1111-1111-1111-111111111111"}},"validade":{"escolha":"exigeDocumentoValido","referencia":{"tipo":"fimDaFase","faseId":"11111111-1111-1111-1111-111111111111"}}}]},"regrasDeComprovacao":[]}""", """{"exigencias":[{"chave":"documento","prazoDeEmissao":{"escolha":"comPrazo","valor":90,"unidade":"dias","referencia":{"tipo":"inicioDaFase","faseChave":"fase"}},"validade":{"escolha":"exigeDocumentoValido","referencia":{"tipo":"fimDaFase","faseChave":"fase"}}}],"regrasDeComprovacao":[]}""")]
    [InlineData("formularios", """{"cronogramaFases":{"fases":[{"id":"11111111-1111-1111-1111-111111111111","chave":"fase"}]},"formularios":[{"finalidade":"INSCRICAO","faseId":"11111111-1111-1111-1111-111111111111","modeloOrigem":{"codigo":"MODELO","nome":"Modelo"},"paginas":[],"termos":[]}],"dadosDosFormularios":[{"finalidade":"INSCRICAO","codigoDoDado":"ESCOLA","ordem":0,"origemValores":"OpcoesDoProcesso","valoresSelecionaveis":[{"valorCodigo":"A","descricao":"Opção","ordem":0}]}],"gruposDosFormularios":[]}""", """[{"finalidade":"INSCRICAO","faseChave":"fase","modeloOrigemCodigo":"MODELO","modeloOrigemNome":"Modelo","paginas":[],"itens":[{"codigoDoDado":"ESCOLA","ordem":0,"opcoes":[{"codigo":"A","rotulo":"Opção"}]}],"grupos":[],"termos":[]}]""")]
    [InlineData("motivosDeDecisao", """{"cronogramaFases":{"fases":[{"id":"11111111-1111-1111-1111-111111111111","chave":"fase","tipoFaseOrigemId":"33333333-3333-3333-3333-333333333333"}]},"etapas":[{"id":"44444444-4444-4444-4444-444444444444","chave":"etapa"}],"motivosDeDecisao":[{"tipoDeDecisaoCodigo":"HOMOLOGACAO_DAS_INSCRICOES","codigo":"MOTIVO","lugar":{"tipo":"FASE","id":"33333333-3333-3333-3333-333333333333"},"descricao":"Motivo","subclassificacao":null,"sentido":"FAVORAVEL","versaoDoEditalDeEntrada":1,"justificativaDaInclusao":null}]}""", """[{"faseChave":"fase","tipoDeDecisaoCodigo":"HOMOLOGACAO_DAS_INSCRICOES","codigo":"MOTIVO","descricao":"Motivo","sentido":"FAVORAVEL","versaoDoEditalDeEntrada":1}]""")]
    [InlineData("coleta", """{"cronogramaFases":{"fases":[{"id":"11111111-1111-1111-1111-111111111111","chave":"fase"}]},"documentosExigidos":{"referenciaTemporalDosDados":{"tipo":"INICIO_FASE","faseId":"11111111-1111-1111-1111-111111111111","data":null}}}""", """{"referenciaTemporalDosDados":{"tipo":"INICIO_FASE","faseChave":"fase"}}""")]
    [InlineData("classificacao", """{"etapas":[{"id":"44444444-4444-4444-4444-444444444444","chave":"etapa"}],"classificacao":{"regraCalculo":{"codigo":"CLASSIFICACAO-IMPORTADA","versao":"v1"},"regraOrdemAlocacao":{"codigo":"ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA","versao":"v1"},"baseadoEmEnem":false,"regrasEliminacao":[]},"criteriosDesempate":[{"ordem":1,"regra":{"codigo":"DESEMPATE-MAIOR-NOTA-ETAPA","versao":"v1"},"parametros":{"etapaId":"44444444-4444-4444-4444-444444444444"}}]}""", """{"regraCalculo":{"codigo":"CLASSIFICACAO-IMPORTADA","versao":"v1"},"regraOrdemAlocacao":{"codigo":"ALOCACAO-PRIMEIRA-OPCAO-PRIORITARIA","versao":"v1"},"baseadoEmEnem":false,"regrasEliminacao":[],"criteriosDeDesempate":[{"ordem":1,"regra":{"codigo":"DESEMPATE-MAIOR-NOTA-ETAPA","versao":"v1"},"parametros":{"etapaChave":"etapa"}}]}""")]
    [InlineData("motivosDeDecisao", """{"etapas":[{"id":"44444444-4444-4444-4444-444444444444","chave":"etapa"}],"motivosDeDecisao":[{"tipoDeDecisaoCodigo":"HOMOLOGACAO_DAS_INSCRICOES","codigo":"MOTIVO","lugar":{"tipo":"ETAPA","id":"44444444-4444-4444-4444-444444444444"},"descricao":"Motivo","sentido":"FAVORAVEL","versaoDoEditalDeEntrada":1}]}""", """[{"etapaChave":"etapa","tipoDeDecisaoCodigo":"HOMOLOGACAO_DAS_INSCRICOES","codigo":"MOTIVO","descricao":"Motivo","sentido":"FAVORAVEL","versaoDoEditalDeEntrada":1}]""")]
    public void Projetar_FragmentoCongelado_RespeitaFormaEEsquema(string nome, string congelado, string esperado)
    {
        JsonObject documento = new ProjecaoDaVersaoPublicada(Esquema).Projetar(new(Guid.Empty, 1, DateTimeOffset.UnixEpoch, congelado));
        JsonNode parte = documento[nome]!;
        parte.ToJsonString().Should().Be(JsonNode.Parse(esperado)!.ToJsonString());
        EsquemaDaParte(nome).Evaluate(JsonSerializer.SerializeToElement(parte), new EvaluationOptions { RequireFormatValidation = true }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("ELIM-NOTA-MINIMA-ETAPA", ",\"notaMinima\":5")]
    [InlineData("ELIM-AUSENCIA-NA-ETAPA", "")]
    public void Projetar_RegraDeEliminacaoComEtapa_UsaChaveDoCongelado(string codigo, string nota)
    {
        string congelado = """{"etapas":[{"id":"44444444-4444-4444-4444-444444444444","chave":"prova"}],"classificacao":{"regrasEliminacao":[{"regra":{"codigo":"CODIGO","versao":"v1"},"parametros":{"etapaId":"44444444-4444-4444-4444-444444444444"@NOTA@}}]}}""".Replace("CODIGO", codigo, StringComparison.Ordinal).Replace("@NOTA@", nota, StringComparison.Ordinal);
        JsonObject documento = new ProjecaoDaVersaoPublicada(Esquema).Projetar(new(Guid.Empty, 1, DateTimeOffset.UnixEpoch, congelado));
        JsonNode classificacao = documento["classificacao"]!;
        JsonNode parametros = classificacao["regrasEliminacao"]![0]!["parametros"]!;
        parametros["etapaChave"]!.GetValue<string>().Should().Be("prova");
        ((JsonObject)parametros).ContainsKey("etapaId").Should().BeFalse();
        EsquemaDaParte("classificacao").Evaluate(JsonSerializer.SerializeToElement(classificacao)).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("ELIM-NOTA-MINIMA-ETAPA")]
    [InlineData("ELIM-AUSENCIA-NA-ETAPA")]
    public void Projetar_RegraDeEliminacaoComEtapaNaoEncontrada_LancaExcecao(string codigo)
    {
        string congelado = """{"etapas":[],"classificacao":{"regrasEliminacao":[{"regra":{"codigo":"CODIGO","versao":"v1"},"parametros":{"etapaId":"44444444-4444-4444-4444-444444444444"}}]}}""".Replace("CODIGO", codigo, StringComparison.Ordinal);
        Action projetar = () => new ProjecaoDaVersaoPublicada(Esquema).Projetar(new(Guid.Empty, 1, DateTimeOffset.UnixEpoch, congelado));
        projetar.Should().Throw<InvalidOperationException>().WithMessage("*44444444-4444-4444-4444-444444444444*etapa*");
    }

    [Theory]
    [InlineData("taxaDeInscricao", """{"taxaInscricao":{"presente":false}}""")]
    [InlineData("cascataRemanejamento", """{"cascataRemanejamento":{"presente":false}}""")]
    public void Projetar_BlocoNaoPresente_OmiteAParte(string nome, string congelado)
    {
        JsonObject documento = new ProjecaoDaVersaoPublicada(Esquema).Projetar(new(Guid.Empty, 1, DateTimeOffset.UnixEpoch, congelado));
        documento.ContainsKey(nome).Should().BeFalse();
    }

    [Fact]
    public void Projetar_OfertaNaoEncontrada_InformaOIdentificador()
    {
        const string congelado = """{"distribuicao":[{"ofertaCursoOrigemId":"11111111-1111-1111-1111-111111111111"}],"ofertas":[]}""";
        Action projetar = () => new ProjecaoDaVersaoPublicada(Esquema).Projetar(new(Guid.Empty, 1, DateTimeOffset.UnixEpoch, congelado));
        projetar.Should().Throw<InvalidOperationException>().WithMessage("*11111111-1111-1111-1111-111111111111*");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[{\"id\":\"11111111-1111-1111-1111-111111111111\",\"tipoFaseOrigemId\":\"33333333-3333-3333-3333-333333333333\",\"chave\":\"a\"},{\"id\":\"22222222-2222-2222-2222-222222222222\",\"tipoFaseOrigemId\":\"33333333-3333-3333-3333-333333333333\",\"chave\":\"b\"}]")]
    public void Projetar_MotivoComTipoDeFaseSemCorrespondenciaUnica_LancaExcecao(string fases)
    {
        string congelado = "{\"cronogramaFases\":{\"fases\":" + fases + "},\"motivosDeDecisao\":[{\"lugar\":{\"tipo\":\"FASE\",\"id\":\"33333333-3333-3333-3333-333333333333\"}}]}";
        Action projetar = () => new ProjecaoDaVersaoPublicada(Esquema).Projetar(new(Guid.Empty, 1, DateTimeOffset.UnixEpoch, congelado));
        projetar.Should().Throw<InvalidOperationException>().WithMessage("*33333333-3333-3333-3333-333333333333*");
    }

    private static JsonSchema EsquemaDaParte(string nome)
    {
        ArquivosDoEsquema arquivos = ArquivosDoEsquema.DoAssembly();
        BuildOptions opcoes = new() { SchemaRegistry = new SchemaRegistry { Fetch = static (_, _) => null } };
        foreach (JsonNode arquivo in arquivos.PorNome.Values)
        {
            JsonSchema.FromText(arquivo.ToJsonString(), opcoes);
        }
        string referencia = $"https://uniplus.unifesspa.edu.br/contracts/selecao/configuracao/v1/documento.schema.json#/properties/{nome}";
        return JsonSchema.FromText($$"""{"$ref":"{{referencia}}"}""", opcoes);
    }
}
