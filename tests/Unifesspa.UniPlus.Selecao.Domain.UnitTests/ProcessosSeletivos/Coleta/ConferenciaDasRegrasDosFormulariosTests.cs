namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Coleta;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Formularios;

/// <summary>
/// Exibição, obrigatoriedade, opções e condição de termo de um campo, de uma página ou de um termo só citam
/// fatos conhecidos no formulário dono, e o conjunto das dependências é acíclico (UNI-REQ-0145,
/// UNI-REQ-0077, UNI-REQ-0086). Dois formulários não dependem um do outro em círculo (UNI-REQ-0144).
/// </summary>
public sealed class ConferenciaDasRegrasDosFormulariosTests
{
    private readonly FaseCronograma _inscricao = FaseDeTeste.Criar(1, "INSCRICAO", coletaInscricao: true);
    private readonly FaseCronograma _habilitacao = FaseDeTeste.Criar(3, "HABILITACAO");

    private ConhecimentoDosFatos Conhecimento(params ColetaDoFormulario[] coletas) =>
        new(coletas, new CatalogoDosFatosCitados(new Dictionary<string, string>(), [], new Dictionary<string, IReadOnlyCollection<string>>()), [_inscricao, _habilitacao]);

    private static ColetaDoFormulario Com(FormularioDeclarado formulario, params ItemDoGrafo[] itens) =>
        new(formulario, itens, [], [], []);

    private static ItemDoGrafo Item(string fato, int ordem, params string[] citados) => new(fato, ordem, null, citados);

    [Fact(DisplayName = "A regra que cita fato do próprio formulário é aceita")]
    public void CitaFatoDoProprioFormulario_Aceita()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);

        ConferenciaDasRegrasDosFormularios.Conferir(Conhecimento(Com(inscricao, Item("origem_escolar", 1), Item("cota", 2, "origem_escolar")))).Should().BeEmpty();
    }

    [Fact(DisplayName = "A regra que cita fato de formulário obrigatório já alcançado é aceita")]
    public void CitaFatoDeFormularioAlcancado_Aceita()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        FormularioDeclarado habilitacao = ColetaDeTeste.Obrigatorio("HABILITACAO", _habilitacao);

        ConferenciaDasRegrasDosFormularios.Conferir(
            Conhecimento(Com(inscricao, Item("origem_escolar", 1)), Com(habilitacao, Item("carteira", 1, "origem_escolar")))).Should().BeEmpty();
    }

    [Fact(DisplayName = "A regra que cita fato que o processo não conhece no formulário é recusada pelo caminho")]
    public void CitaFatoDesconhecido_Recusa()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);

        FieldError recusa = ConferenciaDasRegrasDosFormularios.Conferir(Conhecimento(Com(inscricao, Item("cota", 1, "fato_desconhecido")))).Single();

        recusa.Error.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoNaoConhecido);
        recusa.Field.Should().Be("formularios[INSCRICAO].itens.cota.fatosCitados.fato_desconhecido");
        recusa.Error.Message.Should().NotContain("fato_desconhecido");
    }

    [Fact(DisplayName = "A citação cruzada entre dois formulários da mesma fase é recusada dos dois lados")]
    public void CitacaoCruzadaEntreFormularios_Recusa()
    {
        FormularioDeclarado a = ColetaDeTeste.Obrigatorio("FORMULARIO_A", _inscricao);
        FormularioDeclarado b = ColetaDeTeste.Obrigatorio("FORMULARIO_B", _inscricao);

        ConferenciaDasRegrasDosFormularios.Conferir(
            Conhecimento(Com(a, Item("x", 1, "y")), Com(b, Item("y", 1, "x")))).Select(static recusa => recusa.Field).Should().Equal(
                "formularios[FORMULARIO_A].itens.x.fatosCitados.y", "formularios[FORMULARIO_B].itens.y.fatosCitados.x");
    }

    [Fact(DisplayName = "As regras dos campos do formulário em ciclo são recusadas")]
    public void Ciclo_Recusa()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);

        ConferenciaDasRegrasDosFormularios.Conferir(Conhecimento(Com(inscricao, Item("a", 1, "b"), Item("b", 2, "a"))))
            .Should().ContainSingle().Which.Error.Code.Should().Be(GrafoFormularioErrorCodes.GrafoComCiclo);
    }

    [Fact(DisplayName = "O ciclo que passa por fato derivado é recusado")]
    public void CicloPorDerivado_Recusa()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ConhecimentoDosFatos conhecimento = new(
            [Com(inscricao, Item("renda", 1, "renda_per_capita"))],
            new CatalogoDosFatosCitados(new Dictionary<string, string>(), [], new Dictionary<string, IReadOnlyCollection<string>> { ["renda_per_capita"] = ["renda"] }),
            [_inscricao, _habilitacao]);

        ConferenciaDasRegrasDosFormularios.Conferir(conhecimento).Should().ContainSingle().Which.Error.Code.Should().Be(GrafoFormularioErrorCodes.GrafoComCiclo);
    }

    [Fact(DisplayName = "O ciclo que passa pela exibição da seção do próprio campo é recusado")]
    public void CicloPelaExibicaoDaSecao_Recusa()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ColetaDoFormulario coleta = new(
            inscricao, [new ItemDoGrafo("renda", 1, "DADOS", [])], [], [new PaginaDoGrafo("DADOS", 1, ["renda"])], []);

        ConferenciaDasRegrasDosFormularios.Conferir(Conhecimento(coleta)).Select(static recusa => recusa.Error.Code)
            .Should().Contain(GrafoFormularioErrorCodes.GrafoComCiclo);
    }

    [Fact(DisplayName = "O ciclo pela exibição do grupo, e pela seção do grupo, é recusado")]
    public void CicloPelaExibicaoDoGrupo_Recusa()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        GrupoDoGrafo grupoComExibicao = new("dependentes", 1, null, ["tem_dependente"], [new ItemDoGrafo("tem_dependente", 1, null, [])]);
        GrupoDoGrafo grupoNaSecao = new("irmaos", 2, "FAMILIA", [], [new ItemDoGrafo("nome_do_irmao", 1, null, [])]);
        ColetaDoFormulario coleta = new(inscricao, [], [grupoComExibicao, grupoNaSecao], [new PaginaDoGrafo("FAMILIA", 1, ["nome_do_irmao"])], []);

        ConferenciaDasRegrasDosFormularios.Conferir(Conhecimento(coleta)).Select(static recusa => recusa.Error.Code)
            .Should().Contain(GrafoFormularioErrorCodes.GrafoComCiclo);
    }

    [Fact(DisplayName = "A escolha de fundamentos de dois pedidos é conhecida em cada um deles")]
    public void EscolhaDeFundamentosDeDoisPedidos_ECitadaNoProprioPedido()
    {
        FormularioDeclarado pedidoA = ColetaDeTeste.Facultativo("PEDIDO_A", _inscricao);
        FormularioDeclarado pedidoB = ColetaDeTeste.Facultativo("PEDIDO_B", _inscricao);
        ColetaDoFormulario a = Com(pedidoA, Item("renda_a", 1)) with { FatoDaEscolhaDeFundamentos = "fundamento" };
        ColetaDoFormulario b = Com(pedidoB, Item("renda_b", 1, "fundamento")) with { FatoDaEscolhaDeFundamentos = "fundamento" };

        ConferenciaDasRegrasDosFormularios.Conferir(Conhecimento(a, b)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A exibição de seção e a condição de termo também citam só fatos conhecidos")]
    public void SecaoETermo_CitamSoFatosConhecidos()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        ColetaDoFormulario coleta = new(
            inscricao, [Item("origem_escolar", 1)], [], [new PaginaDoGrafo("DADOS", 1, ["origem_escolar", "desconhecido_a"])], [new TermoDoGrafo("TERMO", ["desconhecido_b"])]);

        ConferenciaDasRegrasDosFormularios.Conferir(Conhecimento(coleta)).Select(static recusa => recusa.Field).Should().Equal(
            "formularios[INSCRICAO].secoes.DADOS.fatosCitados.desconhecido_a", "formularios[INSCRICAO].termos.TERMO.fatosCitados.desconhecido_b");
    }

    [Fact(DisplayName = "O campo de grupo repetível só é citado dentro do próprio grupo")]
    public void CampoDeGrupo_SoNoProprioGrupo()
    {
        FormularioDeclarado inscricao = ColetaDeTeste.Obrigatorio("INSCRICAO", _inscricao);
        GrupoDoGrafo grupo = new("dependentes", 1, null, [], [new ItemDoGrafo("nome", 1, null, []), new ItemDoGrafo("idade_do_dependente", 2, null, ["nome"])]);
        ColetaDoFormulario coleta = new(inscricao, [Item("cota", 2, "nome")], [grupo], [], []);

        ConferenciaDasRegrasDosFormularios.Conferir(Conhecimento(coleta)).Should().ContainSingle()
            .Which.Error.Code.Should().Be(GrafoFormularioErrorCodes.CitaFatoDeMembroForaDoGrupo);
    }
}
