namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.ConteudosDoModeloDeTeste;

/// <summary>O que o conteúdo do modelo enumera das próprias regras: os campos, os fatos que elas citam e as condições.</summary>
public sealed class ConteudoDoModeloRegrasTests
{
    [Fact(DisplayName = "Os campos do modelo são os itens e, depois deles, os campos de cada grupo")]
    public void Campos_ItensEDepoisCamposDeGrupo()
    {
        ConteudoDoModelo conteudo = Vazio with
        {
            Itens = [Item("RENDA"), Item("COR_RACA", 1)],
            Grupos = [Grupo("COMPOSICAO", 0, null, Item("MAIOR_IDADE"), Item("SEM_RENDA", 1)), Grupo("DEPENDENTES", 1, null, Item("PARENTESCO"))],
        };

        conteudo.Campos().Select(static c => c.FatoCodigo).Should().Equal("RENDA", "COR_RACA", "MAIOR_IDADE", "SEM_RENDA", "PARENTESCO");
    }

    [Fact(DisplayName = "O modelo sem itens nem grupos não tem campos")]
    public void Campos_ModeloSemItensNemGrupos_Vazio() => Vazio.Campos().Should().BeEmpty();

    [Fact(DisplayName = "Os fatos citados vêm dos campos, dos campos de grupo, dos grupos, das etapas, dos termos e dos documentos")]
    public void FatosCitados_ReuneTodasAsOrigens()
    {
        ConteudoDoModelo conteudo = Vazio with
        {
            Etapas = [Etapa(exibicao: Quando("DA_ETAPA"))],
            Itens = [Item("RENDA", exibicao: Quando("DO_ITEM"))],
            Grupos = [Grupo("COMPOSICAO", 0, Quando("DO_GRUPO"), Item("MAIOR_IDADE", exibicao: Quando("DO_CAMPO_DE_GRUPO")))],
            Termos = [Termo("LGPD", exibicao: Quando("DO_TERMO"))],
            DocumentosExigidos = [Documento("LAUDO", Quando("DO_DOCUMENTO"))],
        };

        conteudo.FatosCitados().Should().Equal("DO_ITEM", "DO_CAMPO_DE_GRUPO", "DO_GRUPO", "DA_ETAPA", "DO_TERMO", "DO_DOCUMENTO");
    }

    [Fact(DisplayName = "O documento multiplicado por opção cita também o fato das opções")]
    public void FatosCitados_DocumentoPorOpcao_CitaOFatoDasOpcoes()
    {
        ConteudoDoModelo conteudo = Vazio with { DocumentosExigidos = [Documento("LAUDO", porOpcao: new("MODALIDADE", []))] };

        conteudo.FatosCitados().Should().Equal("MODALIDADE");
    }

    [Fact(DisplayName = "O modelo sem regras não cita fato")]
    public void FatosCitados_SemRegras_Vazio() => (Vazio with { Itens = [Item("RENDA")] }).FatosCitados().Should().BeEmpty();

    private static ItemDoModelo CampoComRegras() => Item("CERTIFICADO", exibicao: Quando("DA_EXIBICAO_DO_CAMPO"))
        .ComObrigatoriedade(Obrigatoriedade.Quando(Quando("DA_OBRIGATORIEDADE_DO_CAMPO")))
        .ComImpedimento(new Impedimento(Quando("DO_IMPEDIMENTO"), "Já possui o certificado."));

    private static GrupoDoModelo GrupoComRegras() =>
        Grupo("COMPOSICAO", 0, Quando("DA_EXIBICAO_DO_GRUPO"), Item("MAIOR_IDADE", exibicao: Quando("DO_CAMPO_DE_GRUPO")))
            .ComObrigatoriedade(Obrigatoriedade.Quando(Quando("DA_OBRIGATORIEDADE_DO_GRUPO")));

    private static TermoDoModelo TermoComRegras() =>
        Termo("LGPD", exibicao: Quando("DA_EXIBICAO_DO_TERMO")).ComObrigatoriedade(Obrigatoriedade.Quando(Quando("DA_OBRIGATORIEDADE_DO_TERMO")));

    private static ConteudoDoModelo ConteudoComRegrasEmTodasAsOrigens() => Vazio with
    {
        Etapas = [Etapa(exibicao: Quando("DA_ETAPA"))],
        Itens = [CampoComRegras()],
        Grupos = [GrupoComRegras()],
        Termos = [TermoComRegras()],
        DocumentosExigidos = [Documento("LAUDO", Quando("DO_DOCUMENTO"))],
    };

    [Fact(DisplayName = "As condições vêm das regras de campos, campos de grupo, grupos, etapas, termos e documentos, nessa ordem")]
    public void CondicoesDasRegras_ReuneTodasAsOrigensNaOrdem()
    {
        IEnumerable<string> fatos = ConteudoComRegrasEmTodasAsOrigens().CondicoesDasRegras().Select(static c => c.Fato);

        fatos.Should().Equal(
            "DA_EXIBICAO_DO_CAMPO",
            "DA_OBRIGATORIEDADE_DO_CAMPO",
            "DO_IMPEDIMENTO",
            "DO_CAMPO_DE_GRUPO",
            "DA_EXIBICAO_DO_GRUPO",
            "DA_OBRIGATORIEDADE_DO_GRUPO",
            "DA_ETAPA",
            "DA_EXIBICAO_DO_TERMO",
            "DA_OBRIGATORIEDADE_DO_TERMO",
            "DO_DOCUMENTO");
    }

    [Fact(DisplayName = "O predicado de várias cláusulas contribui com as condições de todas elas, na ordem das cláusulas")]
    public void CondicoesDasRegras_PredicadoComVariasClausulas_TrazTodasAsCondicoes()
    {
        PredicadoDnf composto = PredicadoDnf.CriarDeCondicoesAgrupadas(
        [
            (0, CondicaoDnf.Criar("A", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!),
            (0, CondicaoDnf.Criar("B", Operador.Diferente, JsonSerializer.SerializeToElement(false)).Value!),
            (1, CondicaoDnf.Criar("C", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!),
        ]).Value!;

        IEnumerable<CondicaoDnf> condicoes = (Vazio with { Itens = [Item("RENDA", exibicao: composto)] }).CondicoesDasRegras();

        condicoes.Select(static c => (c.Fato, c.Operador)).Should().Equal(("A", Operador.Igual), ("B", Operador.Diferente), ("C", Operador.Igual));
    }

    [Fact(DisplayName = "A condição das opções permitidas de um campo é condição de regra, com o operador e o valor")]
    public void CondicoesDasRegras_OpcoesPermitidasCondicionadas_TrazOperadorEValor()
    {
        PredicadoDnf quando = Quando("TIPO_ESCOLA", Operador.Diferente, JsonSerializer.SerializeToElement("PRIVADA"));
        ItemDoModelo campo = Item("COTA").ComRestricoes([new OpcoesPermitidas([new OpcoesCondicionadas(quando, ["A", "B"])])]);

        CondicaoDnf condicao = (Vazio with { Itens = [campo] }).CondicoesDasRegras().Should().ContainSingle().Subject;

        condicao.Fato.Should().Be("TIPO_ESCOLA");
        condicao.Operador.Should().Be(Operador.Diferente);
        condicao.Valor.GetString().Should().Be("PRIVADA");
    }

    [Fact(DisplayName = "A condição das opções permitidas vem depois das regras do próprio campo e antes do campo seguinte")]
    public void CondicoesDasRegras_OpcoesPermitidas_DepoisDasRegrasDoCampo()
    {
        ItemDoModelo comOpcoes = Item("COTA", exibicao: Quando("DA_EXIBICAO"))
            .ComRestricoes([new OpcoesPermitidas([new OpcoesCondicionadas(Quando("DAS_OPCOES"), ["A"])])])
            .ComImpedimento(new Impedimento(Quando("DO_IMPEDIMENTO"), "Não pode."));

        IEnumerable<string> fatos = (Vazio with { Itens = [comOpcoes, Item("RENDA", 1, Quando("DO_SEGUNDO"))] }).CondicoesDasRegras().Select(static c => c.Fato);

        fatos.Should().Equal("DA_EXIBICAO", "DO_IMPEDIMENTO", "DAS_OPCOES", "DO_SEGUNDO");
    }

    [Fact(DisplayName = "A opção permitida sem condição não é condição de regra")]
    public void CondicoesDasRegras_OpcoesPermitidasSemCondicao_NaoTemCondicao()
    {
        ItemDoModelo campo = Item("COTA").ComRestricoes([new OpcoesPermitidas([new OpcoesCondicionadas(null, ["A"])])]);

        (Vazio with { Itens = [campo] }).CondicoesDasRegras().Should().BeEmpty();
    }
}
