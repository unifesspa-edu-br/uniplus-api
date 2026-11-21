namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Mappings;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class ModeloFormularioMappingTests
{
    private static readonly Guid TermoId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid VersaoId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact(DisplayName = "O conteúdo com todos os opcionais presentes vira a entrada com cada parte no seu campo")]
    public void ParaEntrada_ConteudoComOpcionaisPresentes_ProjetaCadaParteNoSeuCampo()
    {
        ConteudoDoModeloInput entrada = ConteudoCompleto().ParaEntrada();

        Serializar(entrada).Should().Be(Serializar(EntradaCompleta()));
    }

    [Fact(DisplayName = "O conteúdo com os opcionais ausentes vira a entrada com os opcionais nulos e as listas vazias")]
    public void ParaEntrada_ConteudoComOpcionaisAusentes_ProjetaNuloEListaVazia()
    {
        ConteudoDoModeloInput entrada = ConteudoMinimo().ParaEntrada();

        Serializar(entrada).Should().Be(Serializar(EntradaMinima()));
    }

    [Fact(DisplayName = "O conteúdo sem documentos exigidos vira a entrada com a lista de documentos vazia")]
    public void ParaEntrada_ConteudoSemDocumentosExigidos_ProjetaListaVazia()
    {
        ConteudoDoModeloInput entrada = (ConteudoMinimo() with { DocumentosExigidos = null }).ParaEntrada();

        entrada.DocumentosExigidos.Should().BeEmpty();
    }

    [Fact(DisplayName = "O conteúdo completo na leitura tem o mesmo JSON da entrada que a escrita recebe")]
    public void ToDto_ConteudoCompleto_TemOMesmoJsonDaEntradaDaEscrita()
    {
        ConteudoDoModeloDto leitura = ConteudoCompleto().ToDto();

        JsonNode.DeepEquals(JsonSerializer.SerializeToNode(leitura), JsonSerializer.SerializeToNode(EntradaCompleta())).Should().BeTrue();
    }

    [Fact(DisplayName = "O conteúdo mínimo na leitura traz os blocos presentes: listas vazias, exibição nula e documentos sem condição")]
    public void ToDto_ConteudoMinimo_TrazOsBlocosPresentes()
    {
        ConteudoDoModeloDto leitura = ConteudoMinimo().ToDto();

        leitura.Pressupostos.Should().BeEmpty();
        leitura.Itens.Single().Regras.Exibicao.Should().BeNull();
        leitura.DocumentosExigidos.Single().Quando.Should().BeNull();
        leitura.DocumentosExigidos.Single().PorOpcao.Should().BeNull();
    }

    [Fact(DisplayName = "O conteúdo nulo é recusado")]
    public void ParaEntrada_ConteudoNulo_Lanca()
    {
        Action projetar = () => ((ConteudoDoModelo)null!).ParaEntrada();

        projetar.Should().Throw<ArgumentNullException>();
    }

    private static string Serializar(ConteudoDoModeloInput entrada) => JsonSerializer.Serialize(entrada);

    private static ConteudoDoModelo ConteudoCompleto() => new(
        "Inscrição Medicina",
        [EtapaCompleta(), new EtapaDoModelo("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null)],
        [ItemCompleto()],
        [new TermoDoModelo(
            "TERMO_USO", 0, TermoId, VersaoId,
            new ExibicaoEObrigatoriedade(Quando("QUILOMBOLA", true), Obrigatoriedade.Quando(Quando("BAIXA_RENDA", true))))],
        ["COR_RACA"],
        [GrupoCompleto()],
        [DocumentoCompleto()]);

    private static ConteudoDoModelo ConteudoMinimo() => new(
        null,
        [new EtapaDoModelo("DADOS", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null, null)],
        [ItemMinimo("QUILOMBOLA", 0)],
        [new TermoDoModelo("TERMO_USO", 0, TermoId, VersaoId, new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Sempre))],
        [],
        [new GrupoDoModelo(
            "FAMILIA", 0, null, "Família", new OcorrenciasDoGrupo(1, null, IncluiCandidato: false), new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Nunca), [ItemMinimo("PARENTESCO", 0)])],
        [new DocumentoExigidoDoModelo("INSCRICAO", "RG", true, null, "INDEFERIMENTO", [])]);

    private static EtapaDoModelo EtapaCompleta() => new(
        "DADOS", 0, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", "Dados do candidato", "Confira os dados", Quando("QUILOMBOLA", true));

    private static ItemDoModelo ItemCompleto() => new(
        "RENDA",
        1,
        "DADOS",
        new ApresentacaoDoCampo("Renda familiar", TipoRenderizacao.Numero, "Some a renda de todos", pedirConfirmacao: true),
        "LIVRE",
        new RegrasDoCampo(
            Quando("QUILOMBOLA", true),
            Obrigatoriedade.Quando(Quando("BAIXA_RENDA", true)),
            [new FaixaNumerica(0, 10), new OpcoesDasRespostas(["COR_RACA"])],
            new Impedimento(Quando("BAIXA_RENDA", false), "Renda acima do limite")));

    private static ItemDoModelo ItemMinimo(string fatoCodigo, int ordem) =>
        new(fatoCodigo, ordem, null, new ApresentacaoDoCampo("Pergunta", TipoRenderizacao.Booleano), null, new RegrasDoCampo(null, Obrigatoriedade.Sempre, []));

    private static GrupoDoModelo GrupoCompleto() => new(
        "FAMILIA", 2, "DADOS", "Composição familiar", new OcorrenciasDoGrupo(1, 5, true),
        new ExibicaoEObrigatoriedade(Quando("QUILOMBOLA", true), Obrigatoriedade.Quando(Quando("BAIXA_RENDA", true))),
        [ItemMinimo("PARENTESCO", 0), ItemMinimo("RENDA_MEMBRO", 1)]);

    private static DocumentoExigidoDoModelo DocumentoCompleto() => new(
        "HABILITACAO", "CERTIDAO", false, Quando("QUILOMBOLA", true), "PERDA_DA_VAGA",
        [new BaseLegalDoDocumentoDoModelo("Lei 12.711/2012", "NACIONAL", "VIGENTE", "Art. 3"), new BaseLegalDoDocumentoDoModelo("Portaria 18/2012", "NACIONAL", "VIGENTE", null)],
        new ExigenciaPorOpcao("OPCAO_DE_CURSO", ["MEDICINA"]));

    private static ConteudoDoModeloInput EntradaCompleta() => new(
        "Inscrição Medicina",
        [EtapaCompletaNaEntrada(), EntradaDaEtapaFormularioDeTeste.BlocoDeSistema("REVISAO", 1, "REVISAO_E_ACEITE").ComTitulo("Revisão").Construir()],
        [ItemCompletoNaEntrada()],
        [
            EntradaDoTermoExigidoDeTeste.Exigindo("TERMO_USO").DaVersao(TermoId, VersaoId).ExibidoQuando(QuandoNaEntrada("QUILOMBOLA", true))
                .ComObrigatoriedade("QUANDO", QuandoNaEntrada("BAIXA_RENDA", true)).Construir(),
        ],
        ["COR_RACA"],
        [GrupoCompletoNaEntrada()],
        [DocumentoCompletoNaEntrada()]);

    private static ConteudoDoModeloInput EntradaMinima() => new(
        null,
        [EntradaDaEtapaFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").Construir()],
        [ItemMinimoNaEntrada("QUILOMBOLA", 0)],
        [EntradaDoTermoExigidoDeTeste.Exigindo("TERMO_USO").DaVersao(TermoId, VersaoId).Construir()],
        [],
        [EntradaDoGrupoColetadoDeTeste.Compondo("FAMILIA").ComRotulo("Família").ComOcorrencias(1)
            .ComCampos(ItemMinimoNaEntrada("PARENTESCO", 0)).Construir()],
        [new DocumentoExigidoDoModeloInput("INSCRICAO", "RG", true, null, "INDEFERIMENTO", [])]);

    private static EtapaFormularioInput EtapaCompletaNaEntrada() =>
        EntradaDaEtapaFormularioDeTeste.Secao("DADOS").ComTitulo("Dados").ComDescricao("Dados do candidato").ComAviso("Confira os dados")
            .ExibidaQuando(QuandoNaEntrada("QUILOMBOLA", true)).Construir();

    private static FatoColetadoInput ItemCompletoNaEntrada() =>
        EntradaDoFatoColetadoDeTeste.Coletando("RENDA", 1)
            .ComRotulo("Renda familiar").DoTipo("NUMERO").ComAjuda("Some a renda de todos").PedindoConfirmacao().NaSecao("DADOS")
            .ExibidoQuando(QuandoNaEntrada("QUILOMBOLA", true))
            .ComObrigatoriedade("QUANDO", QuandoNaEntrada("BAIXA_RENDA", true))
            .ComRestricoes(new RestricaoValorInput("FAIXA_NUMERICA", 0, 10), new RestricaoValorInput("OPCOES_DAS_RESPOSTAS", Fatos: ["COR_RACA"]))
            .ComImpedimento(new ImpedimentoInput(QuandoNaEntrada("BAIXA_RENDA", false), "Renda acima do limite"))
            .Construir();

    private static FatoColetadoInput ItemMinimoNaEntrada(string fatoCodigo, int ordem) =>
        EntradaDoFatoColetadoDeTeste.Coletando(fatoCodigo, ordem).ComRotulo("Pergunta").ComRestricoes().Construir();

    private static GrupoColetadoInput GrupoCompletoNaEntrada() =>
        EntradaDoGrupoColetadoDeTeste.Compondo("FAMILIA", 2).ComRotulo("Composição familiar").NaSecao("DADOS")
            .ComOcorrencias(1, 5).IncluindoOCandidato().ExibidoQuando(QuandoNaEntrada("QUILOMBOLA", true))
            .ComObrigatoriedade("QUANDO", QuandoNaEntrada("BAIXA_RENDA", true))
            .ComCampos(ItemMinimoNaEntrada("PARENTESCO", 0), ItemMinimoNaEntrada("RENDA_MEMBRO", 1)).Construir();

    private static DocumentoExigidoDoModeloInput DocumentoCompletoNaEntrada() => new(
        "HABILITACAO", "CERTIDAO", false, QuandoNaEntrada("QUILOMBOLA", true), "PERDA_DA_VAGA",
        [new BaseLegalDoModeloInput("Lei 12.711/2012", "NACIONAL", "VIGENTE", "Art. 3"), new BaseLegalDoModeloInput("Portaria 18/2012", "NACIONAL", "VIGENTE", null)],
        new ExigenciaPorOpcaoInput("OPCAO_DE_CURSO", ["MEDICINA"]));

    private static PredicadoDnf Quando(string fato, bool valor) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!)]).Value!;

    private static IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>> QuandoNaEntrada(string fato, bool valor) =>
        [[new CondicaoPrecondicaoInput(fato, "IGUAL", JsonSerializer.SerializeToElement(valor))]];
}
