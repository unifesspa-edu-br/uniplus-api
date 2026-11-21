namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Retificacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Retificacao.FaseDaRetificacaoDeTeste;

/// <summary>
/// A retificação que muda a regra ou os dados de entrada de um preliminar publicado, ainda com o ciclo aberto,
/// exige declarar o novo ciclo; o bloco pode declarar além do piso e só declara ciclo de preliminar publicado e aberto.
/// </summary>
public sealed class ConferenciaDoCicloNovoDaRetificacaoTests
{
    private const string Materia = "RESULTADO_PROVA";
    private const string OutraMateria = "RESULTADO_ANALISE";
    private static readonly DateTimeOffset UmDiaDepois = Instante.AddDays(1);

    [Fact(DisplayName = "Mudança de prazo na própria fase do preliminar publicado exige declarar o novo ciclo")]
    public void PrazoDaPropriaFase_ExigeDeclararOCiclo()
    {
        Cronograma publicado = new();

        ResultadoDoCicloNovo resultado = publicado.Conferir(publicado.ComPrazoNovoEm(publicado.Prova), [], publicado.ItemPreliminar);

        FieldError recusa = resultado.Recusas.Should().ContainSingle().Subject;
        recusa.Field.Should().BeNull("o ciclo novo é declarado no pedido, e não no documento");
        recusa.Error.Code.Should().Be(RetificacaoErrorCodes.CicloNovoNaoDeclarado);
        recusa.Error.Message.Should().Contain(Materia).And.Contain("exige declarar o novo ciclo");
        resultado.CiclosRecursaisRefeitos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Mudança de prazo em fase de momento anterior ao da fase do preliminar exige declarar o novo ciclo")]
    public void PrazoDeFaseAnterior_ExigeDeclararOCiclo()
    {
        Cronograma publicado = new();

        ResultadoDoCicloNovo resultado = publicado.Conferir(publicado.ComPrazoNovoEm(publicado.Inscricao), [], publicado.ItemPreliminar);

        resultado.Recusas.Should().ContainSingle().Which.Error.Code.Should().Be(RetificacaoErrorCodes.CicloNovoNaoDeclarado);
    }

    [Fact(DisplayName = "Mudança de prazo em fase posterior à do preliminar não exige declarar o novo ciclo")]
    public void PrazoDeFasePosterior_NaoExige()
    {
        Cronograma publicado = new();

        ResultadoDoCicloNovo resultado = publicado.Conferir(
            publicado.ComPrazoNovoEm(publicado.Resultado, publicado.Matricula), [], publicado.ItemPreliminar);

        resultado.Recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Mudar só a ordem e os produtos da fase do preliminar não exige declarar o novo ciclo")]
    public void OrdemEProdutos_NaoExigem()
    {
        Cronograma publicado = new();
        FaseCronograma provaRenomeada = Renomeada(publicado.Prova, publicado.Prova.Codigo, 9, Preliminar(Materia), Preliminar(OutraMateria));

        ResultadoDoCicloNovo resultado = publicado.Conferir(
            [publicado.Inscricao, provaRenomeada, publicado.Resultado, publicado.Matricula], [], publicado.ItemPreliminar);

        resultado.Recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Trocar o código da fase do preliminar exige declarar o novo ciclo")]
    public void CodigoDaFaseDoPreliminar_ExigeDeclararOCiclo()
    {
        Cronograma publicado = new();
        FaseCronograma provaComOutroCodigo = Renomeada(publicado.Prova, "PROVA_OBJETIVA", publicado.Prova.Ordem, Preliminar(Materia));

        ResultadoDoCicloNovo resultado = publicado.Conferir(
            [publicado.Inscricao, provaComOutroCodigo, publicado.Resultado, publicado.Matricula], [], publicado.ItemPreliminar);

        resultado.Recusas.Should().ContainSingle().Which.Error.Code.Should().Be(RetificacaoErrorCodes.CicloNovoNaoDeclarado);
    }

    [Fact(DisplayName = "Declarar o ciclo de um preliminar publicado e aberto fora do piso é aceito e sai nos ciclos refeitos")]
    public void DeclararAlemDoPiso_EAceito()
    {
        Cronograma publicado = new();

        ResultadoDoCicloNovo resultado = publicado.Conferir(publicado.Fases, [publicado.ItemPreliminar], publicado.ItemPreliminar);

        resultado.Recusas.Should().BeEmpty();
        resultado.CiclosRecursaisRefeitos.Should().Equal(publicado.ItemPreliminar);
    }

    [Fact(DisplayName = "Declarar o ciclo que o piso exige não recusa e sai nos ciclos refeitos")]
    public void DeclararOPiso_Passa()
    {
        Cronograma publicado = new();

        ResultadoDoCicloNovo resultado = publicado.Conferir(
            publicado.ComPrazoNovoEm(publicado.Prova), [publicado.ItemPreliminar], publicado.ItemPreliminar);

        resultado.Recusas.Should().BeEmpty();
        resultado.CiclosRecursaisRefeitos.Should().Equal(publicado.ItemPreliminar);
    }

    [Fact(DisplayName = "Declarar o ciclo de um preliminar que não foi publicado é recusado")]
    public void DeclararPreliminarNaoPublicado_ERecusado()
    {
        Cronograma publicado = new();

        ResultadoDoCicloNovo resultado = publicado.Conferir(publicado.Fases, [publicado.ItemPreliminar]);

        FieldError recusa = resultado.Recusas.Should().ContainSingle().Subject;
        recusa.Field.Should().BeNull("o ciclo novo é declarado no pedido, e não no documento");
        recusa.Error.Code.Should().Be(RetificacaoErrorCodes.CicloNovoDeItemSemPreliminarAberto);
        resultado.CiclosRecursaisRefeitos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Com o definitivo já publicado a mudança não exige novo ciclo, e declarar o ciclo é recusado")]
    public void CicloFechado_NaoExigeERecusaDeclarar()
    {
        Cronograma publicado = new();
        FaseCronograma[] novas = publicado.ComPrazoNovoEm(publicado.Prova);

        ResultadoDoCicloNovo semDeclarar = publicado.Conferir(novas, [], publicado.ItemPreliminar, publicado.ItemDefinitivo);
        ResultadoDoCicloNovo declarado = publicado.Conferir(novas, [publicado.ItemPreliminar], publicado.ItemPreliminar, publicado.ItemDefinitivo);

        semDeclarar.Recusas.Should().BeEmpty();
        declarado.Recusas.Should().ContainSingle().Which.Error.Code.Should().Be(RetificacaoErrorCodes.CicloNovoDeItemSemPreliminarAberto);
        declarado.CiclosRecursaisRefeitos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Preliminar publicado sem definitivo no cronograma segue elegível: a mudança exige declarar e declarar é aceito")]
    public void PreliminarSemDefinitivoNoCronograma_SegueElegivel()
    {
        ProdutoDaFase preliminar = Preliminar(Materia);
        FaseCronograma prova = Fase(2, "PROVA", produtos: [preliminar]);
        IdentidadeDoItemDoEdital item = Item(prova, preliminar);
        FaseCronograma[] novas = [ComJanela(prova, UmDiaDepois, UmDiaDepois.AddDays(8))];

        ResultadoDoCicloNovo semDeclarar = ConferenciaDoCicloNovoDaRetificacao.Conferir([prova], [], novas, [], new HashSet<IdentidadeDoItemDoEdital>(), Fatos(Iniciadas(), item));
        ResultadoDoCicloNovo declarado = ConferenciaDoCicloNovoDaRetificacao.Conferir([prova], [], novas, [], new HashSet<IdentidadeDoItemDoEdital> { item }, Fatos(Iniciadas(), item));

        semDeclarar.Recusas.Should().ContainSingle().Which.Error.Code.Should().Be(RetificacaoErrorCodes.CicloNovoNaoDeclarado);
        declarado.Recusas.Should().BeEmpty();
        declarado.CiclosRecursaisRefeitos.Should().Equal(item);
    }

    [Fact(DisplayName = "Declarar item que não existe na versão publicada é recusado")]
    public void DeclararItemInexistente_ERecusado()
    {
        Cronograma publicado = new();
        FaseCronograma foraDoCronograma = Fase(8, "OUTRA", produtos: [Preliminar(Materia)]);
        IdentidadeDoItemDoEdital inexistente = Item(foraDoCronograma, foraDoCronograma.Produtos.Single());

        ResultadoDoCicloNovo resultado = publicado.Conferir(publicado.Fases, [inexistente], publicado.ItemPreliminar);

        resultado.Recusas.Should().ContainSingle().Which.Error.Code.Should().Be(RetificacaoErrorCodes.CicloNovoDeItemSemPreliminarAberto);
    }

    [Fact(DisplayName = "Preliminar de etapa com o peso da etapa alterado exige declarar o novo ciclo")]
    public void PesoDaEtapaDoPreliminar_ExigeDeclararOCiclo()
    {
        Guid etapaId = Guid.CreateVersion7();
        EtapaProcesso publicada = EtapaDaProva(etapaId, peso: 1m);
        EtapaProcesso nova = EtapaDaProva(etapaId, peso: 2m);
        FaseCronograma prova = Fase(1, "PROVA");
        IdentidadeDoItemDoEdital item = PreliminarDaEtapa(publicada);

        ResultadoDoCicloNovo recusado = ConferenciaDoCicloNovoDaRetificacao.Conferir([prova], [publicada], [prova], [nova], new HashSet<IdentidadeDoItemDoEdital>(), Fatos(Iniciadas(), item));
        ResultadoDoCicloNovo declarado = ConferenciaDoCicloNovoDaRetificacao.Conferir([prova], [publicada], [prova], [nova], new HashSet<IdentidadeDoItemDoEdital> { item }, Fatos(Iniciadas(), item));

        recusado.Recusas.Should().ContainSingle().Which.Field.Should().BeNull();
        declarado.Recusas.Should().BeEmpty();
        declarado.CiclosRecursaisRefeitos.Should().Equal(item);
    }

    [Fact(DisplayName = "Trocar só o procedimento de recurso da etapa do preliminar exige declarar o novo ciclo")]
    public void ProcedimentoDeRecursoDaEtapaDoPreliminar_ExigeDeclararOCiclo()
    {
        Guid etapaId = Guid.CreateVersion7();
        EtapaProcesso publicada = EtapaDaProvaComProcedimento(etapaId, RegraProcedimentoRecursoCodigo.Recalculo);
        EtapaProcesso nova = EtapaDaProvaComProcedimento(etapaId, RegraProcedimentoRecursoCodigo.Recorrecao);
        FaseCronograma prova = Fase(1, "PROVA");
        IdentidadeDoItemDoEdital item = PreliminarDaEtapa(publicada);

        ResultadoDoCicloNovo trocado = ConferenciaDoCicloNovoDaRetificacao.Conferir([prova], [publicada], [prova], [nova], new HashSet<IdentidadeDoItemDoEdital>(), Fatos(Iniciadas(), item));
        ResultadoDoCicloNovo mantido = ConferenciaDoCicloNovoDaRetificacao.Conferir(
            [prova], [publicada], [prova], [EtapaDaProvaComProcedimento(etapaId, RegraProcedimentoRecursoCodigo.Recalculo)], new HashSet<IdentidadeDoItemDoEdital>(), Fatos(Iniciadas(), item));

        trocado.Recusas.Should().ContainSingle().Which.Error.Code.Should().Be(RetificacaoErrorCodes.CicloNovoNaoDeclarado);
        mantido.Recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Preliminar de etapa com mudança numa fase de momento anterior à fase da etapa exige declarar o novo ciclo")]
    public void FaseAnteriorAFaseDaEtapa_ExigeDeclararOCiclo()
    {
        EtapaProcesso etapa = EtapaDaProva(Guid.CreateVersion7(), peso: 1m);
        FaseCronograma inscricao = Fase(1, "INSCRICAO");
        FaseCronograma prova = Fase(2, "PROVA");
        FaseCronograma[] novas = [ComJanela(inscricao, UmDiaDepois, UmDiaDepois.AddDays(8)), prova];
        FatosDaExecucao fatos = Fatos(Iniciadas(), PreliminarDaEtapa(etapa));

        ResultadoDoCicloNovo resultado = ConferenciaDoCicloNovoDaRetificacao.Conferir([inscricao, prova], [etapa], novas, [etapa], new HashSet<IdentidadeDoItemDoEdital>(), fatos);

        resultado.Recusas.Should().ContainSingle().Which.Error.Code.Should().Be(RetificacaoErrorCodes.CicloNovoNaoDeclarado);
    }

    [Fact(DisplayName = "A mudança na própria fase da etapa do preliminar exige declarar o novo ciclo")]
    public void FaseDaEtapa_ExigeDeclararOCiclo()
    {
        EtapaProcesso etapa = EtapaDaProva(Guid.CreateVersion7(), peso: 1m);
        FaseCronograma prova = Fase(1, "PROVA");
        FaseCronograma[] novas = [ComJanela(prova, UmDiaDepois, UmDiaDepois.AddDays(8))];
        FatosDaExecucao fatos = Fatos(Iniciadas(), PreliminarDaEtapa(etapa));

        ResultadoDoCicloNovo resultado = ConferenciaDoCicloNovoDaRetificacao.Conferir([prova], [etapa], novas, [etapa], new HashSet<IdentidadeDoItemDoEdital>(), fatos);

        resultado.Recusas.Should().ContainSingle().Which.Error.Code.Should().Be(RetificacaoErrorCodes.CicloNovoNaoDeclarado);
    }

    [Fact(DisplayName = "Mudança em fase de mesmo momento que a do preliminar não exige declarar o novo ciclo")]
    public void FaseDeMesmoMomento_NaoExige()
    {
        ProdutoDaFase preliminar = Preliminar(Materia);
        FaseCronograma isencao = Fase(1, "ISENCAO", coletaIsencao: true);
        FaseCronograma inscricao = Fase(2, "INSCRICAO", coletaInscricao: true, produtos: [preliminar]);
        FaseCronograma[] novas = [ComJanela(isencao, UmDiaDepois, UmDiaDepois.AddDays(8)), inscricao];

        ResultadoDoCicloNovo resultado = ConferenciaDoCicloNovoDaRetificacao.Conferir(
            [isencao, inscricao], [], novas, [], new HashSet<IdentidadeDoItemDoEdital>(), Fatos(Iniciadas(), Item(inscricao, preliminar)));

        resultado.Recusas.Should().BeEmpty();
    }

    [Fact(DisplayName = "As recusas se acumulam: primeiro o piso não declarado e depois o declarado sem preliminar aberto, cada grupo em ordem da chave")]
    public void Recusas_AcumulamEmOrdem()
    {
        Cronograma publicado = new(comSegundaMateria: true);
        IdentidadeDoItemDoEdital[] definitivosDeclarados = [publicado.ItemDefinitivoDaSegundaMateria, publicado.ItemDefinitivo];
        IdentidadeDoItemDoEdital[] piso = [publicado.ItemPreliminar, publicado.ItemPreliminarDaSegundaMateria];

        ResultadoDoCicloNovo resultado = publicado.Conferir(
            publicado.ComPrazoNovoEm(publicado.Prova), definitivosDeclarados, piso);

        resultado.Recusas.Select(static r => r.Error.Code).Should().Equal(
            RetificacaoErrorCodes.CicloNovoNaoDeclarado, RetificacaoErrorCodes.CicloNovoNaoDeclarado,
            RetificacaoErrorCodes.CicloNovoDeItemSemPreliminarAberto, RetificacaoErrorCodes.CicloNovoDeItemSemPreliminarAberto);
        resultado.Recusas.Select(static r => r.Field).Should().AllSatisfy(static campo => campo.Should().BeNull());
        resultado.Recusas.Select(static r => r.Error.Message).Zip([.. AtosEmOrdem(piso), .. AtosEmOrdem(definitivosDeclarados)])
            .Should().OnlyContain(static par => par.First.Contains($"'{par.Second}'", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Fase do lugar do preliminar ausente no cronograma novo não exige declarar o ciclo aqui")]
    public void FaseDoLugarAusenteNaNova_NaoExigeAqui()
    {
        Cronograma publicado = new();

        ResultadoDoCicloNovo resultado = publicado.Conferir(
            [publicado.Inscricao, publicado.Resultado, publicado.Matricula], [], publicado.ItemPreliminar);

        resultado.Recusas.Should().BeEmpty();
    }

    private static IEnumerable<string> AtosEmOrdem(IEnumerable<IdentidadeDoItemDoEdital> itens) =>
        itens.OrderBy(static i => i.Chave, StringComparer.Ordinal).Select(static i => i.AtoCodigo);

    private static IdentidadeDoItemDoEdital PreliminarDaEtapa(EtapaProcesso etapa) =>
        IdentidadeDoItemDoEdital.DaEtapa(etapa, etapa.Produtos.Single(static p => p.Papel == PapelProdutoFase.Preliminar));

    private static EtapaProcesso EtapaDaProva(Guid id, decimal peso) =>
        EtapaComPeso(id, "Prova objetiva", peso, "PROVA", ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Preliminar), ProdutoDaEtapa.Criar(Materia, PapelProdutoFase.Definitivo));

    private static EtapaProcesso EtapaDaProvaComProcedimento(Guid id, string regraDoProcedimento)
    {
        EtapaProcesso etapa = EtapaDaProva(id, peso: 1m);
        RecursoDaEtapa recurso = RecursoDaEtapa.Criar(
            AncoraDoRecurso.AtoPublicado,
            ReferenciaRegra.De(RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", new string('c', 64)),
            ParametrosDoRecursoDeTeste.Declarados(2m, UnidadePrazo.DiasUteis),
            etapa.Produtos.Single(static p => p.Papel == PapelProdutoFase.Preliminar).Id,
            ProcedimentosDeRecurso.De([ProcedimentoDeRecursoDeclarado.De(
                TipoMotivoDeDecisao.Classificacao, null, ReferenciaRegra.De(regraDoProcedimento, "v1", new string('a', 64)))])).Value!;
        etapa.DefinirRecursos([recurso]).IsSuccess.Should().BeTrue();

        return etapa;
    }

    /// <summary>
    /// A versão publicada: inscrição, prova (que publica o preliminar e aponta o resultado como concluinte),
    /// resultado (que publica o definitivo) e matrícula, em momentos crescentes.
    /// </summary>
    private sealed class Cronograma
    {
        private readonly ProdutoDaFase _preliminar = Preliminar(Materia);
        private readonly ProdutoDaFase _definitivo = Definitivo(Materia);
        private readonly ProdutoDaFase _preliminarDaSegundaMateria = Preliminar(OutraMateria);
        private readonly ProdutoDaFase _definitivoDaSegundaMateria = Definitivo(OutraMateria);

        internal Cronograma(bool comSegundaMateria = false)
        {
            Inscricao = Fase(1, "INSCRICAO");
            Prova = Fase(2, "PROVA", faseConcluinteCodigo: "RESULTADO", produtos: comSegundaMateria ? [_preliminar, _preliminarDaSegundaMateria] : [_preliminar]);
            Resultado = Fase(3, "RESULTADO", produtos: comSegundaMateria ? [_definitivo, _definitivoDaSegundaMateria] : [_definitivo]);
            Matricula = Fase(4, "MATRICULA");
        }

        internal FaseCronograma Inscricao { get; }

        internal FaseCronograma Prova { get; }

        internal FaseCronograma Resultado { get; }

        internal FaseCronograma Matricula { get; }

        internal FaseCronograma[] Fases => [Inscricao, Prova, Resultado, Matricula];

        internal IdentidadeDoItemDoEdital ItemPreliminar => Item(Prova, _preliminar);

        internal IdentidadeDoItemDoEdital ItemPreliminarDaSegundaMateria => Item(Prova, _preliminarDaSegundaMateria);

        internal IdentidadeDoItemDoEdital ItemDefinitivo => Item(Resultado, _definitivo);

        internal IdentidadeDoItemDoEdital ItemDefinitivoDaSegundaMateria => Item(Resultado, _definitivoDaSegundaMateria);

        /// <summary>O cronograma novo em que só as fases dadas têm a janela trocada.</summary>
        internal FaseCronograma[] ComPrazoNovoEm(params FaseCronograma[] fases) =>
            [.. Fases.Select(fase => fases.Contains(fase) ? ComJanela(fase, UmDiaDepois, UmDiaDepois.AddDays(8)) : fase)];

        internal ResultadoDoCicloNovo Conferir(FaseCronograma[] novas, IdentidadeDoItemDoEdital[] declarados, params IdentidadeDoItemDoEdital[] publicados) =>
            ConferenciaDoCicloNovoDaRetificacao.Conferir(
                Fases, [], novas, [], new HashSet<IdentidadeDoItemDoEdital>(declarados), Fatos(Iniciadas(), publicados));
    }
}
