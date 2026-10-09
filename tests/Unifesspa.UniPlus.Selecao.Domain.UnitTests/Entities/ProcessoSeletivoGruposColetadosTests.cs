namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Os grupos repetíveis do formulário no processo (UNI-REQ-0146): o grupo é dono dos seus campos,
/// que são fatos de membro — nunca itens, nunca citados fora do grupo — e entra nas mesmas
/// conferências dos itens: grafo, teto, produtor único, seções e remoção.
/// </summary>
public sealed class ProcessoSeletivoGruposColetadosTests
{
    [Fact]
    public void DefinirFatosColetados_ComGrupo_GuardaOGrupoComOsCamposForaDosItens()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirItens([Item("RENDA", 0)], grupos: [Grupo("COMPOSICAO", 1, Campo("PARENTESCO", 0))]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.FatosColetados.ForaDoConjuntoBasico().Select(static f => f.FatoCodigo).Should().Equal("RENDA");
        GrupoColetado grupo = processo.GruposColetados.Should().ContainSingle().Subject;
        grupo.ProcessoSeletivoId.Should().Be(processo.Id);
        grupo.Subitens.Should().ContainSingle().Which.Finalidade.Should().Be(FinalidadeFormulario.Inscricao);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public void DefinirFatosColetados_GruposOmitidosFicam_ListaVaziaRemove(bool listaVazia, int esperados)
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens([Item("RENDA", 0)], grupos: [Grupo("COMPOSICAO", 1, Campo("PARENTESCO", 0))]).IsSuccess.Should().BeTrue();

        processo.DefinirItens([Item("RENDA", 0)], grupos: listaVazia ? [] : null).IsSuccess.Should().BeTrue();

        processo.GruposColetados.Should().HaveCount(esperados);
        processo.Campos.ForaDoConjuntoBasico().Should().HaveCount(1 + esperados);
    }

    [Fact]
    public void DefinirFatosColetados_ItemQueCitaCampoDoGrupo_Recusa()
    {
        Result resultado = NovoProcesso().DefinirItens(
            [Item("RENDA", 2, citado: "PARENTESCO")], grupos: [Grupo("COMPOSICAO", 1, Campo("PARENTESCO", 0))]);

        resultado.Error!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoDeMembroForaDoGrupo);
    }

    [Fact]
    public void DefinirFatosColetados_GrupoECamposContamNoTeto()
    {
        FatoColetado[] itens = [.. Enumerable.Range(0, FormaDoItem.MaximoDeItens - 1).Select(static i => Item($"ITEM_{i}", i))];

        Result resultado = NovoProcesso().DefinirItens(itens, grupos: [Grupo("COMPOSICAO", FormaDoItem.MaximoDeItens, Campo("PARENTESCO", 0))]);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(ItemFormularioErrorCodes.ItensEmExcesso);
    }

    [Fact]
    public void DefinirFatosColetados_CampoDeGrupoJaProduzidoEmOutraFinalidade_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens([], grupos: [Grupo("COMPOSICAO", 0, Campo("PARENTESCO", 0))]).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirItens(
            [], finalidade: FinalidadeFormulario.Habilitacao, grupos: [Grupo("FAMILIA", 0, Campo("PARENTESCO", 0))]);

        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.FatoDuplicado);
    }

    [Theory]
    [InlineData("COMPOSICAO", null)]
    [InlineData("RENDA", null)]
    [InlineData("FAMILIA", "COMPOSICAO")]
    public void DefinirFatosColetados_CodigoDeGrupoRepetidoNoProcesso_Recusa(string codigoDoGrupo, string? item)
    {
        // Habilitação e isenção não conhecem os fatos uma da outra: a recusa é a do processo, não a do grafo.
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens(
            [Item("RENDA", 0)], finalidade: FinalidadeFormulario.Habilitacao, grupos: [Grupo("COMPOSICAO", 1, Campo("PARENTESCO", 0))])
            .IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirItens(
            item is null ? [] : [Item(item, 0)],
            finalidade: FinalidadeFormulario.IsencaoTaxa,
            grupos: [Grupo(codigoDoGrupo, 1, Campo("OUTRO_CAMPO", 0))]);

        resultado.Error!.Code.Should().Be(FatoColetadoErrorCodes.FatoDuplicado);
    }

    [Fact]
    public void DefinirFatosColetados_GrupoForaDeSecao_Recusa()
    {
        Result resultado = NovoProcesso().DefinirItens([], grupos: [Grupo("COMPOSICAO", 0, secao: "INEXISTENTE", campos: Campo("PARENTESCO", 0))]);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.ItemForaDeSecao);
    }

    [Fact]
    public void DefinirFormulario_EtapasSemASecaoDoGrupo_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens([], grupos: [Grupo("COMPOSICAO", 0, Campo("PARENTESCO", 0))]).IsSuccess.Should().BeTrue();
        EtapaFormulario[] semADeDados =
        [
            FormularioDeTeste.Etapas()[0],
            EtapaFormulario.Criar("REVISAO", 1, TipoEtapaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null).Value!,
        ];

        Result resultado = processo.DefinirFormulario(FinalidadeFormulario.Inscricao, null, null, semADeDados, PrecondicaoIfMatch.Ausente);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.ItemForaDeSecao);
    }

    [Fact]
    public void DefinirFatosColetados_InscricaoSemFatoCitadoPorCampoDeGrupoDaHabilitacao_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens([Item("CONCORRER_RENDA", 0)]).IsSuccess.Should().BeTrue();
        processo.DefinirItens(
            [], finalidade: FinalidadeFormulario.Habilitacao,
            grupos: [Grupo("COMPOSICAO", 0, Campo("RENDA_DO_MEMBRO", 0, citado: "CONCORRER_RENDA"))]).IsSuccess.Should().BeTrue();

        Result resultado = processo.DefinirItens([]);

        resultado.Error!.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
    }

    [Fact]
    public void RemoverFormulario_RemoveOsGruposDaFinalidade()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens([], grupos: [Grupo("COMPOSICAO", 0, Campo("PARENTESCO", 0))]).IsSuccess.Should().BeTrue();

        processo.RemoverFormulario(FinalidadeFormulario.Inscricao, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.GruposColetados.Should().BeEmpty();
    }

    [Fact]
    public void AplicarModeloDeFormulario_SubstituiOFormularioInteiro_InclusiveOsGrupos()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens([], grupos: [Grupo("COMPOSICAO", 0, Campo("PARENTESCO", 0))]).IsSuccess.Should().BeTrue();
        CopiaDeModeloDeFormulario copia = new(
            FinalidadeFormulario.Inscricao, "Formulário", FormularioDeTeste.Etapas(), [Item("RENDA", 0)], [], [], [], Guid.CreateVersion7(), "MODELO");

        processo.AplicarModeloDeFormulario(copia, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.GruposColetados.Should().BeEmpty();
    }

    [Fact]
    public void AplicarModeloDeFormulario_CopiaComGrupo_GravaOGrupoComOsCampos()
    {
        ProcessoSeletivo processo = NovoProcesso();
        CopiaDeModeloDeFormulario copia = new(
            FinalidadeFormulario.Inscricao, "Formulário", FormularioDeTeste.Etapas(), [Item("RENDA", 0)],
            [Grupo("COMPOSICAO", 1, Campo("PARENTESCO", 0), Campo("MAIOR_IDADE", 1))], [], [], Guid.CreateVersion7(), "MODELO");

        processo.AplicarModeloDeFormulario(copia, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        processo.GruposColetados.Single().Subitens.Select(static s => s.FatoCodigo).Should().Equal("PARENTESCO", "MAIOR_IDADE");
        processo.FatosColetados.ForaDoConjuntoBasico().Select(static f => f.FatoCodigo).Should().Equal("RENDA");
    }

    [Fact]
    public void AplicarModeloDeFormulario_ItemCitaCampoDoGrupoDaCopia_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        CopiaDeModeloDeFormulario copia = new(
            FinalidadeFormulario.Inscricao, "Formulário", FormularioDeTeste.Etapas(), [Item("RENDA", 2, citado: "PARENTESCO")],
            [Grupo("COMPOSICAO", 0, Campo("PARENTESCO", 0))], [], [], Guid.CreateVersion7(), "MODELO");

        processo.AplicarModeloDeFormulario(copia, PrecondicaoIfMatch.Ausente)
            .Errors.Should().ContainSingle().Which.Error.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoDeMembroForaDoGrupo);
    }

    [Fact]
    public void AplicarModeloDeFormulario_CampoDeGrupoColetadoPorOutroFormulario_Recusa()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens([Item("PARENTESCO", 0)]).IsSuccess.Should().BeTrue();
        CopiaDeModeloDeFormulario copia = new(
            FinalidadeFormulario.Habilitacao, "Formulário", FormularioDeTeste.Etapas(), [],
            [Grupo("COMPOSICAO", 0, Campo("PARENTESCO", 0))], [], [], Guid.CreateVersion7(), "MODELO");

        processo.AplicarModeloDeFormulario(copia, PrecondicaoIfMatch.Ausente)
            .Errors.Should().ContainSingle().Which.Should().Match<FieldError>(static e => e.Field == "grupos[0]" && e.Error.Code == FatoColetadoErrorCodes.FatoDuplicado);
    }

    [Theory]
    [InlineData("COMPOSICAO")]
    [InlineData("PARENTESCO")]
    public void AplicarModeloDeFormulario_ItemComCodigoDeGrupoDeOutroFormulario_Recusa(string codigo)
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens([], finalidade: FinalidadeFormulario.Habilitacao, grupos: [Grupo("COMPOSICAO", 0, Campo("PARENTESCO", 0))])
            .IsSuccess.Should().BeTrue();
        CopiaDeModeloDeFormulario copia = new(
            FinalidadeFormulario.Inscricao, "Formulário", FormularioDeTeste.Etapas(), [Item(codigo, 0)], [], [], [], Guid.CreateVersion7(), "MODELO");

        Result resultado = processo.AplicarModeloDeFormulario(copia, PrecondicaoIfMatch.Ausente);

        resultado.Errors.Should().ContainSingle().Which.Error.Code.Should().Be(FatoColetadoErrorCodes.FatoDuplicado);
        processo.GruposColetados.Single().Subitens.Should().ContainSingle("a recusa deixa o grupo como estava");
    }

    [Theory]
    [InlineData("COMPOSICAO")]
    [InlineData("PARENTESCO")]
    public void DefinirRegrasDerivacao_ComCodigoDeGrupoOuDeCampo_Recusa(string codigo)
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens([Item("RENDA", 0)], grupos: [Grupo("COMPOSICAO", 1, Campo("PARENTESCO", 0))]).IsSuccess.Should().BeTrue();
        ConfiguracaoDerivacaoFato derivacao = ConfiguracaoDerivacaoFato.Criar(
            codigo,
            [RegraDerivacaoConfigurada.Criar(0, "SIM", [CondicaoRegraDerivacao.Criar(0, "RENDA", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!]).Value!])
            .Value!;

        Result resultado = processo.DefinirRegrasDerivacao([derivacao], PrecondicaoIfMatch.Ausente);

        resultado.Error!.Code.Should().Be(ConfiguracaoDerivacaoFatoErrorCodes.CodigoFatoDuplicado);
    }

    [Fact]
    public void Vinculos_IncluemOsCamposEOsValoresCitadosPeloGrupo()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens(
            [Item("COR_RACA", 0)],
            grupos: [Grupo("COMPOSICAO", 1, exibicao: Predicado("COR_RACA", "PRETA"), campos: Campo("PARENTESCO", 0))]).IsSuccess.Should().BeTrue();

        VinculosDeFatos vinculos = processo.Vinculos();

        vinculos.Fatos.Should().Contain("PARENTESCO");
        vinculos.Valores.Should().Contain(("COR_RACA", "PRETA"));
    }

    [Fact]
    public void ConstruirGrafoDependencia_CampoDoGrupo_EhGatadoPelasRegrasDoGrupo()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirItens(
            [Item("CONCORRER_RENDA", 0)],
            grupos: [Grupo("COMPOSICAO", 1, exibicao: Predicado("CONCORRER_RENDA", true), campos: Campo("PARENTESCO", 0))]).IsSuccess.Should().BeTrue();

        GrafoDependenciaConjunta grafo = processo.ConstruirGrafoDependencia().Value!;

        grafo.Arestas.Should().Contain(new ArestaGrafoDependencia(
            TipoArestaGrafo.Precondicao, new NoGrafoDependencia(ClasseNoGrafo.Fato, "CONCORRER_RENDA"), new NoGrafoDependencia(ClasseNoGrafo.Campo, "PARENTESCO")));
    }

    private static ProcessoSeletivo NovoProcesso() => ProcessoSeletivo.Criar(
        "PS Grupos", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna, Guid.NewGuid(),
        UnidadeAdministradoraSnapshot.Criar("CEPS", "ceps", "Centro de Processos Seletivos", "ADMINISTRATIVA").Value!,
        LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!, IdentificadoresDeTeste.Novo());

    private static FatoColetado Item(string fato, int ordem, string? citado = null) => FatoColetado.Criar(
        fato, ordem, fato, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, Precondicao(citado)).Value!;

    private static FatoColetado Campo(string fato, int ordem, string? citado = null) => FatoColetado.Criar(
        fato, ordem, fato, TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, Precondicao(citado)).Value!;

    private static CondicaoPrecondicaoFato[]? Precondicao(string? citado) =>
        citado is null ? null : [CondicaoPrecondicaoFato.Criar(0, citado, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!];

    private static PredicadoDnf Predicado(string fato, object valor) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!)]).Value!;

    private static GrupoColetado Grupo(
        string codigo, int ordem, params FatoColetado[] campos) => Grupo(codigo, ordem, secao: FormularioDeTeste.Secao, exibicao: null, campos: campos);

    private static GrupoColetado Grupo(
        string codigo, int ordem, string secao = FormularioDeTeste.Secao, PredicadoDnf? exibicao = null, params FatoColetado[] campos) =>
        GrupoColetado.Criar(codigo, ordem, secao, "Composição familiar", 0, 5, exibicao, Obrigatoriedade.Nunca, campos).Value!;
}
