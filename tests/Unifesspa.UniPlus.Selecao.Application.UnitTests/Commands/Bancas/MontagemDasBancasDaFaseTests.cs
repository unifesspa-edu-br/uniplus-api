namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.Bancas;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class MontagemDasBancasDaFaseTests
{
    private static readonly TipoBancaView TipoDocumental = new(Guid.CreateVersion7(), TipoDeBanca.AnaliseDocumental, "Banca de análise documental", null, null, PapelDaBanca.AvaliaOMerito, false, false, [TipoMotivoDeDecisao.AnaliseDocumental, TipoMotivoDeDecisao.IsencaoDaTaxa]);
    private static readonly TipoBancaView TipoDeRecursos = new(Guid.CreateVersion7(), TipoDeBanca.AnaliseDeRecursos, "Banca de análise de recursos", null, null, PapelDaBanca.JulgaAPrimeiraInstancia, false, false, []);
    private static readonly TipoBancaView TipoDeSegundaInstancia = new(Guid.CreateVersion7(), TipoDeBanca.AnaliseDeRecursosDeSegundaInstancia, "Banca de análise de recursos de 2ª instância", null, null, PapelDaBanca.JulgaASegundaInstancia, false, false, []);
    private static readonly TipoBancaView TipoDeHomologacao = new(Guid.CreateVersion7(), TipoDeBanca.HomologacaoDasInscricoes, "Banca de homologação das inscrições", null, null, PapelDaBanca.AvaliaOMerito, false, false, [TipoMotivoDeDecisao.HomologacaoDasInscricoes]);
    private static readonly CategoriaDocumentoView CategoriaRenda = new(Guid.CreateVersion7(), "RENDA", "Renda", null, 1);

    [Fact(DisplayName = "A banca requerida congela o tipo, os candidatos atendidos e a regra declarada")]
    public async Task Montar_ComCandidatosAtendidosERegra_CongelaOTipoEOsCandidatosAtendidosEARegra()
    {
        MontagemDasBancasDaFase montagem = Montagem();
        BancaRequeridaInput declarada = new(TipoDocumental.Id, [CategoriaRenda.Id], RegrasDeDeliberacaoDeTeste.Valida());

        Result<List<BancaRequerida>> resultado = await montagem.MontarAsync(Fase(declarada), 0, [], CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        BancaRequerida banca = resultado.Value!.Should().ContainSingle().Subject;
        banca.Tipo.Should().Be(new TipoDeBancaCopiado(
            TipoDocumental.Id, TipoDeBanca.AnaliseDocumental, "Banca de análise documental", PapelDaBanca.AvaliaOMerito, false, false,
            [TipoMotivoDeDecisao.AnaliseDocumental, TipoMotivoDeDecisao.IsencaoDaTaxa]));
        banca.CandidatosAtendidos.Select(static categoria => categoria.Codigo).Should().Equal("RENDA");
        banca.RegraDeDeliberacao!.Quorum.Should().Be(2);
    }

    [Fact(DisplayName = "A banca de análise de recursos com candidatos atendidos é montada como veio: quem a recusa é a fábrica da fase")]
    public async Task Montar_BancaDeRecursosComCandidatosAtendidos_MontaSemRecusar()
    {
        BancaRequeridaInput declarada = new(TipoDeRecursos.Id, [CategoriaRenda.Id], RegrasDeDeliberacaoDeTeste.Valida());

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(declarada), 2, [], CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Single().CandidatosAtendidos.Select(static categoria => categoria.Codigo).Should().Equal("RENDA");
    }

    [Fact(DisplayName = "As violações da regra de deliberação das bancas da fase acumulam sob o caminho de cada uma")]
    public async Task Montar_ComVariasBancasInvalidas_AcumulaTudoSobOCaminhoDeCada()
    {
        FaseCronogramaInput fase = Fase(
            new BancaRequeridaInput(TipoDocumental.Id, [], RegrasDeDeliberacaoDeTeste.ComTresViolacoes()),
            new BancaRequeridaInput(TipoDocumental.Id, [CategoriaRenda.Id], RegrasDeDeliberacaoDeTeste.Valida()));

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(fase, 0, [], CancellationToken.None);

        resultado.Errors.Select(static erro => erro.Field).Should().BeEquivalentTo(
        [
            "fases[0].bancasRequeridas[0].regraDeDeliberacao.integrantes",
            "fases[0].bancasRequeridas[0].regraDeDeliberacao.criterio",
            "fases[0].bancasRequeridas[0].regraDeDeliberacao.encerramento",
        ]);
    }

    [Fact(DisplayName = "O tipo de banca ausente do cadastro é recusado no campo do tipo")]
    public async Task Montar_ComTipoAusenteDoCadastro_RecusaNoCampoDoTipo()
    {
        BancaRequeridaInput desconhecida = new(Guid.CreateVersion7(), [], null);

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(desconhecida), 1, [], CancellationToken.None);

        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("fases[1].bancasRequeridas[0].tipoBancaId");
        recusa.Error.Code.Should().Be("FaseCronograma.TipoBancaNaoEncontrado");
    }

    [Fact(DisplayName = "O tipo ausente do cadastro não esconde a categoria ausente, a regra inválida nem a consequência desconhecida")]
    public async Task Montar_ComTipoAusenteERecusasIndependentes_AcumulaTodas()
    {
        BancaRequeridaInput desconhecida = new(Guid.CreateVersion7(), [Guid.CreateVersion7()], RegrasDeDeliberacaoDeTeste.ComTresViolacoes(), ConsequenciaDaAusencia: "INVALIDA");

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(desconhecida), 0, [], CancellationToken.None);

        resultado.Errors.Select(static erro => erro.Field).Should().BeEquivalentTo(
            "fases[0].bancasRequeridas[0].tipoBancaId", "fases[0].bancasRequeridas[0].categoriasDocumentoIds[0]",
            "fases[0].bancasRequeridas[0].regraDeDeliberacao.integrantes", "fases[0].bancasRequeridas[0].regraDeDeliberacao.criterio",
            "fases[0].bancasRequeridas[0].regraDeDeliberacao.encerramento", "fases[0].bancasRequeridas[0].consequenciaDaAusencia");
    }

    [Fact(DisplayName = "A categoria ausente do cadastro é recusada no campo dos candidatos atendidos")]
    public async Task Montar_ComCategoriaAusenteDoCadastro_RecusaNoCampoDosCandidatosAtendidos()
    {
        BancaRequeridaInput declarada = new(TipoDocumental.Id, [Guid.CreateVersion7()], null);

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(declarada), 0, [], CancellationToken.None);

        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("fases[0].bancasRequeridas[0].categoriasDocumentoIds[0]");
        recusa.Error.Code.Should().Be("FaseCronograma.CategoriaDocumentoNaoEncontrada");
    }

    [Fact(DisplayName = "A categoria ausente e a consequência desconhecida da mesma banca são recusadas juntas")]
    public async Task Montar_ComCategoriaAusenteEConsequenciaDesconhecida_AcumulaAsDuasRecusas()
    {
        BancaRequeridaInput declarada = new(TipoDocumental.Id, [Guid.CreateVersion7()], null, ConsequenciaDaAusencia: "CONSEQUENCIA_INEXISTENTE");

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(declarada), 0, [], CancellationToken.None);

        resultado.Errors.Select(recusa => recusa.Field).Should().BeEquivalentTo(
            "fases[0].bancasRequeridas[0].categoriasDocumentoIds[0]",
            "fases[0].bancasRequeridas[0].consequenciaDaAusencia");
    }

    [Fact(DisplayName = "A categoria ausente do cadastro é recusada na posição que ela ocupa na lista")]
    public async Task Montar_ComSegundaCategoriaAusenteDoCadastro_RecusaNaPosicaoDela()
    {
        BancaRequeridaInput declarada = new(TipoDocumental.Id, [CategoriaRenda.Id, Guid.CreateVersion7()], null);

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(declarada), 0, [], CancellationToken.None);

        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("fases[0].bancasRequeridas[0].categoriasDocumentoIds[1]");
    }

    [Fact(DisplayName = "A banca sem regra é rascunho e a montagem a aceita")]
    public async Task Montar_SemRegra_AceitaORascunho()
    {
        BancaRequeridaInput declarada = new(TipoDocumental.Id, [CategoriaRenda.Id], null);

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(declarada), 0, [], CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Single().RegraDeDeliberacao.Should().BeNull();
    }

    [Fact(DisplayName = "A banca de homologação que decide só a homologação, em ato individual sem votação, é montada")]
    public async Task Montar_HomologacaoEmAtoIndividualSemVotacao_Aceita()
    {
        BancaRequeridaInput declarada = new(TipoDeHomologacao.Id, [], RegrasDeDeliberacaoDeTeste.AtoIndividual());

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(declarada), 0, [], CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Single().RegraDeDeliberacao!.Quorum.Should().BeNull();
    }

    [Fact(DisplayName = "A banca de homologação em ato individual com a votação do recurso inteira é montada com o quórum")]
    public async Task Montar_HomologacaoEmAtoIndividualComVotacaoInteira_Aceita()
    {
        BancaRequeridaInput declarada = new(TipoDeHomologacao.Id, [], RegrasDeDeliberacaoDeTeste.AtoIndividualComVotacaoDoRecurso());

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(declarada), 0, [], CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Single().RegraDeDeliberacao!.QuorumExigido.Should().Be(2);
    }

    [Fact(DisplayName = "A banca de homologação com a votação do recurso pela metade recusa o campo que falta, no caminho da banca")]
    public async Task Montar_HomologacaoEmAtoIndividualComVotacaoPelaMetade_RecusaOsCamposQueFaltam()
    {
        BancaRequeridaInput declarada = new(TipoDeHomologacao.Id, [], RegrasDeDeliberacaoDeTeste.AtoIndividualSoComOQuorum());

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(declarada), 0, [], CancellationToken.None);

        resultado.Errors.Select(static erro => erro.Field).Should().BeEquivalentTo(
        [
            "fases[0].bancasRequeridas[0].regraDeDeliberacao.criterio",
            "fases[0].bancasRequeridas[0].regraDeDeliberacao.encerramento",
        ]);
        resultado.Errors.Should().OnlyContain(static erro => erro.Error.Code == "Banca.VotacaoDoRecursoIncompleta");
    }

    [Fact(DisplayName = "A banca de recursos em ato individual com a votação inteira é montada")]
    public async Task Montar_BancaDeRecursosEmAtoIndividualComVotacaoInteira_Aceita()
    {
        BancaRequeridaInput declarada = new(TipoDeRecursos.Id, [], RegrasDeDeliberacaoDeTeste.AtoIndividualComVotacaoDoRecurso());

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(declarada), 0, [], CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "A banca nova do tipo de 2ª instância copia do cadastro o tipo e o papel que julga o recurso em 2ª instância")]
    public async Task Montar_BancaNovaDeSegundaInstancia_CopiaOPapel()
    {
        BancaRequeridaInput declarada = new(TipoDeSegundaInstancia.Id, [], RegrasDeDeliberacaoDeTeste.AtoIndividualComVotacaoDoRecurso());

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(declarada), 0, [], CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Single().Tipo.Should().Be(new TipoDeBancaCopiado(
            TipoDeSegundaInstancia.Id, TipoDeBanca.AnaliseDeRecursosDeSegundaInstancia, "Banca de análise de recursos de 2ª instância",
            PapelDaBanca.JulgaASegundaInstancia, false, false, []));
    }

    [Fact(DisplayName = "A banca de 2ª instância com a votação do recurso pela metade recusa o campo que falta, no caminho da banca")]
    public async Task Montar_SegundaInstanciaEmAtoIndividualComVotacaoPelaMetade_RecusaOsCamposQueFaltam()
    {
        BancaRequeridaInput declarada = new(TipoDeSegundaInstancia.Id, [], RegrasDeDeliberacaoDeTeste.AtoIndividualSoComOQuorum());

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(declarada), 0, [], CancellationToken.None);

        resultado.Errors.Select(static erro => erro.Field).Should().BeEquivalentTo(
        [
            "fases[0].bancasRequeridas[0].regraDeDeliberacao.criterio",
            "fases[0].bancasRequeridas[0].regraDeDeliberacao.encerramento",
        ]);
        resultado.Errors.Should().OnlyContain(static erro => erro.Error.Code == "Banca.VotacaoDoRecursoIncompleta");
    }

    [Fact(DisplayName = "A banca gravada que a declaração substitui sem trocar o tipo mantém a cópia, ainda que o cadastro tenha mudado")]
    public async Task Montar_BancaGravadaDoMesmoTipo_MantemACopiaSemLerOCadastro()
    {
        BancaRequerida gravada = BancaRequerida.Criar(ChaveLocal.Criar("banca").Value,
            new TipoDeBancaCopiado(TipoDocumental.Id, TipoDeBanca.AnaliseDocumental, "Nome de quando a banca nasceu", PapelDaBanca.ImportaCandidatos, true, true, []), []);
        BancaRequeridaInput declarada = new(TipoDocumental.Id, [], null, gravada.Id);

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(declarada), 0, [gravada], CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Single().Tipo.Should().BeSameAs(gravada.Tipo);
    }

    [Fact(DisplayName = "O tipo que saiu do cadastro não afeta a banca gravada que o mantém")]
    public async Task Montar_BancaGravadaComTipoQueSaiuDoCadastro_NaoRecusa()
    {
        Guid tipoDesativado = Guid.CreateVersion7();
        BancaRequerida gravada = BancaRequerida.Criar(ChaveLocal.Criar("banca").Value,
            new TipoDeBancaCopiado(tipoDesativado, TipoDeBanca.Entrevista, "Banca de entrevista", PapelDaBanca.AvaliaOMerito, false, false, [TipoMotivoDeDecisao.NotaDaEtapa]), []);

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(
            Fase(new BancaRequeridaInput(tipoDesativado, [], null, gravada.Id)), 0, [gravada], CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Single().Tipo.Should().BeSameAs(gravada.Tipo);
    }

    [Fact(DisplayName = "A banca gravada que troca de tipo copia o cadastro atual do tipo novo")]
    public async Task Montar_BancaGravadaQueTrocaDeTipo_CopiaOCadastroAtual()
    {
        BancaRequerida gravada = BancaRequerida.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.Entrevista), []);

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(
            Fase(new BancaRequeridaInput(TipoDeRecursos.Id, [], null, gravada.Id)), 0, [gravada], CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Single().Tipo.OrigemId.Should().Be(TipoDeRecursos.Id);
        resultado.Value!.Single().Tipo.Papel.Should().Be(PapelDaBanca.JulgaAPrimeiraInstancia);
    }

    [Fact(DisplayName = "A banca nova com tipo que o cadastro não tem é recusada sem citar o id recebido")]
    public async Task Montar_BancaNovaComTipoDesativado_RecusaSemCitarOId()
    {
        Guid desativado = Guid.CreateVersion7();

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(new BancaRequeridaInput(desativado, [], null)), 0, [], CancellationToken.None);

        resultado.Errors.Should().ContainSingle().Which.Error.Message
            .Should().Be("O tipo de banca indicado não existe ou está desativado no cadastro.");
    }

    private static FaseCronogramaInput Fase(params BancaRequeridaInput[] bancas) => new(
        Ordem: 1, FaseCanonicaId: Guid.CreateVersion7(), Inicio: null, Fim: null, Produtos: [],
        EmiteParecerIndividual: false, BancasRequeridas: bancas, RegraRecurso: null);

    private static MontagemDasBancasDaFase Montagem() => new(new CadastrosDasFases(
        Substitute.For<IFaseCanonicaReader>(),
        Substitute.For<IResolvedorDaUnidadeResponsavel>(),
        TiposDeBanca(),
        Categorias(),
        Substitute.For<IRegraCatalogoReader>(),
        Substitute.For<IFatoCandidatoReader>()), new ChavesDasBancasDoProcesso());

    private static ITipoBancaReader TiposDeBanca()
    {
        ITipoBancaReader leitor = Substitute.For<ITipoBancaReader>();
        leitor.ObterPorIdAsync(TipoDocumental.Id, Arg.Any<CancellationToken>()).Returns(TipoDocumental);
        leitor.ObterPorIdAsync(TipoDeRecursos.Id, Arg.Any<CancellationToken>()).Returns(TipoDeRecursos);
        leitor.ObterPorIdAsync(TipoDeHomologacao.Id, Arg.Any<CancellationToken>()).Returns(TipoDeHomologacao);
        leitor.ObterPorIdAsync(TipoDeSegundaInstancia.Id, Arg.Any<CancellationToken>()).Returns(TipoDeSegundaInstancia);
        return leitor;
    }

    private static ICategoriaDocumentoReader Categorias()
    {
        ICategoriaDocumentoReader leitor = Substitute.For<ICategoriaDocumentoReader>();
        leitor.ObterPorIdAsync(CategoriaRenda.Id, Arg.Any<CancellationToken>()).Returns(CategoriaRenda);
        return leitor;
    }
}
