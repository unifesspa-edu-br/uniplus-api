namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static ModeloDeEtapa;

public sealed class EtapasDoProcessoTests
{
    private static (string? Campo, string Codigo)[] Conferir(params EtapaDeclarada[] etapas) =>
        [.. new EtapasDoProcesso(etapas).Conferir().Select(static r => (r.Field, r.Error.Code))];

    private static ModeloDeEtapa DoEnem(string nome = "Nota do ENEM") => new ModeloDeEtapa(nome).DoEnem();

    [Fact(DisplayName = "Conjunto de etapas conformes não tem causa de conjunto, e o conjunto vazio é válido")]
    public void Conferir_EtapasConformes_SemCausas()
    {
        Conferir(new ModeloDeEtapa("A").Declarar(), new ModeloDeEtapa("B").Declarar()).Should().BeEmpty();
        Conferir().Should().BeEmpty();
    }

    [Fact(DisplayName = "O mesmo identificador em mais de uma etapa é recusado a partir da segunda ocorrência")]
    public void Conferir_IdRepetido_RecusaDaSegundaEmDiante()
    {
        Guid id = Guid.CreateVersion7();

        Conferir(
            (new ModeloDeEtapa("A") with { Id = id }).Declarar(),
            (new ModeloDeEtapa("B") with { Id = id }).Declarar(),
            (new ModeloDeEtapa("C") with { Id = id }).Declarar())
            .Should().Equal(("etapas[1].id", "ProcessoSeletivo.IdEtapaDuplicado"), ("etapas[2].id", "ProcessoSeletivo.IdEtapaDuplicado"));
    }

    [Fact(DisplayName = "As posições de identificador repetido ignoram o que não foi informado")]
    public void PosicoesComIdRepetido_IgnoraNulos()
    {
        Guid id = Guid.CreateVersion7();

        EtapasDoProcesso.PosicoesComIdRepetido([id, null, null, id, Guid.CreateVersion7()]).Should().Equal(3);
        EtapasDoProcesso.IdRepetido().Code.Should().Be("ProcessoSeletivo.IdEtapaDuplicado");
        EtapasDoProcesso.IdRepetido().Message.Should().Be("O mesmo identificador de etapa aparece em mais de uma etapa do processo — cada etapa tem identificador próprio.");
    }

    [Fact(DisplayName = "Ordem repetida é recusada a partir da segunda ocorrência, e a ordem recusada não conta")]
    public void Conferir_OrdemRepetida_RecusaDaSegundaEmDiante()
    {
        Conferir(
            (new ModeloDeEtapa("A") with { Ordem = 1 }).Declarar(),
            (new ModeloDeEtapa("B") with { Ordem = 1 }).Declarar(),
            (new ModeloDeEtapa("C") with { Ordem = 0 }).Declarar(),
            (new ModeloDeEtapa("D") with { Ordem = 0 }).Declarar())
            .Should().Equal(("etapas[1].ordem", "ProcessoSeletivo.OrdemEtapaDuplicada"));
    }

    [Fact(DisplayName = "Duas etapas do ENEM, uma sem peso: a duplicidade vem primeiro e depois a que não compõe a nota")]
    public void Conferir_DuasEtapasDoEnemUmaSemPeso_DuplicadaEDepoisNaoCompoe()
    {
        Conferir(DoEnem("ENEM 1").Declarar(), (DoEnem("ENEM 2") with { Peso = null }).Declarar())
            .Should().Equal(("etapas[1]", "ProcessoSeletivo.EtapaNotaEnemDuplicada"), ("etapas[1].peso", "ProcessoSeletivo.EtapaNotaEnemNaoCompoeNota"));
    }

    [Fact(DisplayName = "ENEM sem peso e com banca acusa as duas causas")]
    public void Conferir_EnemSemPesoEComBanca_AcusaAsDuas()
    {
        Conferir((DoEnem() with { Peso = null, Bancas = [BancasDeTeste.DaEtapa(TipoDeBanca.CorrecaoDeRedacoes)] }).Declarar(), new ModeloDeEtapa("Prova").Declarar())
            .Should().Equal(("etapas[0].peso", "ProcessoSeletivo.EtapaNotaEnemNaoCompoeNota"), ("etapas[0].bancas", "ProcessoSeletivo.EtapaNotaEnemComBanca"));
    }

    [Fact(DisplayName = "ENEM com banca que avalia o mérito é recusado, citando a posição da etapa")]
    public void Conferir_EnemComBancaQueAvalia_Recusa()
    {
        EtapasDoProcesso etapas = new([(DoEnem() with { Bancas = [BancasDeTeste.DaEtapa(TipoDeBanca.CorrecaoDeRedacoes)] }).Declarar()]);

        FieldError recusa = etapas.Conferir().Single();

        recusa.Error.Code.Should().Be("ProcessoSeletivo.EtapaNotaEnemComBanca");
        recusa.Error.Message.Should().Be(
            "A etapa na posição 1 tem a nota vinda do ENEM e não admite banca com o papel 'Avalia o mérito'. Na etapa cabem a banca com o papel 'Importa as notas do INEP' e a banca com o papel 'Julga o recurso em 1ª instância', que julga o recurso individual da etapa. O recurso contra o resultado é declarado na configuração do recurso da fase que publica o resultado.");
    }

    [Theory(DisplayName = "ENEM admite a banca que importa as notas do INEP e a banca que julga o recurso em 1ª instância")]
    [InlineData(TipoDeBanca.ImportacaoDasNotasDoInep)]
    [InlineData(TipoDeBanca.AnaliseDeRecursos)]
    public void Conferir_EnemComBancaQueNaoAvalia_Admite(TipoDeBanca tipo) =>
        Conferir((DoEnem() with { Bancas = [BancasDeTeste.DaEtapa(tipo)] }).Declarar()).Should().BeEmpty();

    [Theory(DisplayName = "ENEM com mais de uma banca é recusado quando uma delas atribui a nota, inclusive a de homologação das inscrições")]
    [InlineData(TipoDeBanca.CorrecaoDeRedacoes)]
    [InlineData(TipoDeBanca.HomologacaoDasInscricoes)]
    public void Conferir_EnemComVariasBancasUmaAvalia_Recusa(TipoDeBanca avaliadora) =>
        Conferir((DoEnem() with
        {
            Bancas = [BancasDeTeste.DaEtapa(TipoDeBanca.ImportacaoDasNotasDoInep), BancasDeTeste.DaEtapa(avaliadora), BancasDeTeste.DaEtapa(TipoDeBanca.AnaliseDeRecursos)],
        }).Declarar())
            .Should().Equal(("etapas[0].bancas", "ProcessoSeletivo.EtapaNotaEnemComBanca"));

    [Fact(DisplayName = "ENEM com banca e com produto acusa as duas causas, e recurso ancorado em ato conta como produto")]
    public void Conferir_EnemComBancaEComProduto_AcusaAsDuas()
    {
        EtapaDeclarada comBancaEProduto = (DoEnem() with { Bancas = [BancasDeTeste.DaEtapa(TipoDeBanca.CorrecaoDeRedacoes)], Produtos = [Preliminar("RESULTADO")] }).Declarar();
        EtapaDeclarada comRecursoEmAto = (DoEnem() with { Recursos = [RecursoModelo.AoAto(Guid.CreateVersion7())] }).Declarar();

        Conferir(comBancaEProduto).Should().Equal(("etapas[0].bancas", "ProcessoSeletivo.EtapaNotaEnemComBanca"), ("etapas[0].produtos", "ProcessoSeletivo.EtapaNotaEnemComProdutoOuRecursoEmAto"));
        Conferir(comRecursoEmAto).Select(static r => r.Codigo).Should().Contain("ProcessoSeletivo.EtapaNotaEnemComProdutoOuRecursoEmAto");
    }

    [Fact(DisplayName = "ENEM com produto é recusado citando a posição da etapa, e não o nome")]
    public void Conferir_EnemComProduto_CitaAPosicaoDaEtapa()
    {
        EtapaDeclarada comProduto = (DoEnem() with { Produtos = [Preliminar("RESULTADO")] }).Declarar();

        new EtapasDoProcesso([comProduto]).Conferir().Single(static r => r.Error.Code == "ProcessoSeletivo.EtapaNotaEnemComProdutoOuRecursoEmAto").Error.Message
            .Should().StartWith("A etapa na posição 1 tem a nota vinda do ENEM e não declara produto nem recurso contra ato publicado:");
    }

    [Fact(DisplayName = "ENEM sem peso quando nenhuma outra etapa compõe a nota acusa só a própria causa")]
    public void Conferir_EnemSemPesoSemOutraQueCompoe_AcusaSoANaoCompoe()
    {
        Conferir((DoEnem() with { Peso = null }).Declarar())
            .Should().Equal(("etapas[0].peso", "ProcessoSeletivo.EtapaNotaEnemNaoCompoeNota"));
    }

    [Fact(DisplayName = "ENEM com caráter recusado não é acusado de não compor: o dado recusado corta a regra")]
    public void Conferir_EnemComCaraterRecusado_CortaANaoCompoe()
    {
        Conferir((DoEnem() with { Carater = CaraterEtapa.Nenhum }).Declarar(), new ModeloDeEtapa("Prova").Declarar()).Should().BeEmpty();
    }

    [Fact(DisplayName = "Nenhuma etapa compondo a nota é recusado no conjunto, e etapa com dado cortado suspende a regra")]
    public void Conferir_NenhumaComponeNota_RecusaNoConjunto()
    {
        EtapaDeclarada eliminatoria = new ModeloDeEtapa("Triagem").SemNota().Declarar();
        EtapaDeclarada comCaraterRecusado = (new ModeloDeEtapa("Prova") with { Carater = CaraterEtapa.Nenhum }).Declarar();

        Conferir(eliminatoria).Should().Equal(("etapas", "ProcessoSeletivo.NenhumaEtapaComponeNota"));
        Conferir(eliminatoria, comCaraterRecusado).Should().BeEmpty();
    }

    [Fact(DisplayName = "Os assuntos do conjunto saem na ordem: identificador, ordem, ENEM e nenhuma compõe")]
    public void Conferir_VariosAssuntos_SaemNaOrdemDosAssuntos()
    {
        Guid id = Guid.CreateVersion7();

        Conferir(
            (DoEnem("ENEM 1") with { Id = id, Ordem = 1, Peso = null }).Declarar(),
            (DoEnem("ENEM 2") with { Id = id, Ordem = 1, Peso = null }).Declarar())
            .Select(static r => r.Codigo).Should().Equal(
                "ProcessoSeletivo.IdEtapaDuplicado",
                "ProcessoSeletivo.OrdemEtapaDuplicada",
                "ProcessoSeletivo.EtapaNotaEnemDuplicada",
                "ProcessoSeletivo.EtapaNotaEnemNaoCompoeNota",
                "ProcessoSeletivo.EtapaNotaEnemNaoCompoeNota");
    }

    [Fact(DisplayName = "A etapa de nota do ENEM é a única do processo, e duas não designam nenhuma")]
    public void EtapaDeNotaDoEnem_SoQuandoUnica()
    {
        EtapaDeclarada enem = DoEnem().Declarar();

        new EtapasDoProcesso([new ModeloDeEtapa("A").Declarar(), enem]).EtapaDeNotaDoEnem.Should().BeSameAs(enem);
        new EtapasDoProcesso([enem, DoEnem("Outra").Declarar()]).EtapaDeNotaDoEnem.Should().BeNull();
        new EtapasDoProcesso([enem]).Etapa(enem.Id).Should().BeSameAs(enem);
        new EtapasDoProcesso([enem]).Etapa(Guid.CreateVersion7()).Should().BeNull();
    }

    [Fact(DisplayName = "Preliminar sem definitivo do mesmo tipo na etapa é recusado nos produtos da etapa")]
    public void ConferirFechamentoDoCiclo_PreliminarSemDefinitivo_Recusa()
    {
        EtapasDoProcesso etapas = new(
        [
            new ModeloDeEtapa("A") { Produtos = [Preliminar("GABARITO"), Definitivo("GABARITO")] }.Declarar(),
            new ModeloDeEtapa("B") { Produtos = [Preliminar("RESULTADO"), Preliminar("GABARITO"), Definitivo("GABARITO")] }.Declarar(),
        ]);

        etapas.ConferirFechamentoDoCiclo().Select(static r => (r.Field, r.Error.Code))
            .Should().Equal(("etapas[1].produtos", "ProcessoSeletivo.EtapaComPreliminarSemDefinitivo"));
    }

    [Fact(DisplayName = "Toda etapa sem fase declarada é recusada na completude, na ordem das posições")]
    public void ConferirCompletude_EtapasSemFase_RecusaCadaUma()
    {
        EtapasDoProcesso etapas = new(
        [
            (new ModeloDeEtapa("Z") with { Ordem = 9 }).Declarar(),
            (new ModeloDeEtapa("A") with { Ordem = 1, FaseCodigo = "AVALIACAO" }).Declarar(),
            (new ModeloDeEtapa("B") with { Ordem = 2 }).Declarar(),
        ]);

        etapas.ConferirCompletude().Select(static r => (r.Field, r.Error.Code)).Should().Equal(
            ("etapas[0].faseCodigo", "ProcessoSeletivo.EtapaSemFaseDeclarada"),
            ("etapas[2].faseCodigo", "ProcessoSeletivo.EtapaSemFaseDeclarada"));
    }

    [Fact(DisplayName = "As recusas das declarações saem prefixadas pela posição da etapa e podem ser filtradas por código")]
    public void RecusasDasDeclaracoes_PrefixamEFiltram()
    {
        EtapasDoProcesso etapas = new(
        [
            new ModeloDeEtapa("A").Declarar(),
            (new ModeloDeEtapa(" ") with { Peso = -1m }).Declarar(),
        ]);

        etapas.RecusasDasDeclaracoes().Select(static r => r.Field).Should().Equal("etapas[1].nome", "etapas[1].peso");
        etapas.RecusasDasDeclaracoes(new HashSet<string> { "EtapaProcesso.PesoInvalido" }).Should().ContainSingle().Which.Field.Should().Be("etapas[1].peso");
    }

    [Fact(DisplayName = "A âncora dos recursos da etapa fica vermelha quando recusada e verde quando ancora num preliminar")]
    public void ItemDaAncoraDoRecurso_ProjetaAsRecusasDaAncora()
    {
        ProdutoDaEtapa preliminar = Preliminar("GABARITO");
        ProdutoDaEtapa definitivo = Definitivo("GABARITO");
        EtapaDeclarada conforme = (new ModeloDeEtapa("A") with { Produtos = [preliminar], Recursos = [RecursoModelo.AoAto(preliminar.Id)] }).Declarar();
        EtapaDeclarada ancoraErrada = (new ModeloDeEtapa("B") with { Produtos = [definitivo], Recursos = [RecursoModelo.AoAto(definitivo.Id)] }).Declarar();
        EtapaDeclarada cienciaSemParecer = (new ModeloDeEtapa("C") with { Recursos = [RecursoModelo.NaCiencia()] }).Declarar();

        new EtapasDoProcesso([conforme]).ItemDaAncoraDoRecurso().Should().Match<Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ItemConformidade>(
            static item => item.Ok && item.Codigo == "cronograma_ancora_do_recurso_da_etapa");
        new EtapasDoProcesso([conforme, ancoraErrada]).ItemDaAncoraDoRecurso().Ok.Should().BeFalse();
        new EtapasDoProcesso([cienciaSemParecer]).ItemDaAncoraDoRecurso().Ok.Should().BeFalse();
    }
}
