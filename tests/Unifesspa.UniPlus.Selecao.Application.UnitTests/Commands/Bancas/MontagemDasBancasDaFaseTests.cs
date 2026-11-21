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
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class MontagemDasBancasDaFaseTests
{
    private static readonly TipoBancaView TipoDocumental = new(Guid.CreateVersion7(), TipoDeBanca.AnaliseDocumental, "Banca de análise documental", null, null, PapelDaBanca.AvaliaOMerito, false, false, [TipoMotivoDeDecisao.AnaliseDocumental, TipoMotivoDeDecisao.IsencaoDaTaxa]);
    private static readonly TipoBancaView TipoDeRecursos = new(Guid.CreateVersion7(), TipoDeBanca.AnaliseDeRecursos, "Banca de análise de recursos", null, null, PapelDaBanca.JulgaAPrimeiraInstancia, false, false, []);
    private static readonly TipoBancaView TipoDeHomologacao = new(Guid.CreateVersion7(), TipoDeBanca.HomologacaoDasInscricoes, "Banca de homologação das inscrições", null, null, PapelDaBanca.AvaliaOMerito, false, false, [TipoMotivoDeDecisao.HomologacaoDasInscricoes]);
    private static readonly CategoriaDocumentoView CategoriaRenda = new(Guid.CreateVersion7(), "RENDA", "Renda", null, 1);

    [Fact(DisplayName = "A banca requerida congela o tipo, os candidatos atendidos e a regra declarada")]
    public async Task Montar_ComCandidatosAtendidosERegra_CongelaOTipoEOsCandidatosAtendidosEARegra()
    {
        MontagemDasBancasDaFase montagem = Montagem();
        BancaRequeridaInput declarada = new(TipoDocumental.Id, [CategoriaRenda.Id], RegrasDeDeliberacaoDeTeste.Valida());

        Result<List<BancaRequerida>> resultado = await montagem.MontarAsync(Fase(declarada), 0, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        BancaRequerida banca = resultado.Value!.Should().ContainSingle().Subject;
        banca.Codigo.Should().Be("BANCA_ANALISE_DOCUMENTAL");
        banca.Nome.Should().Be("Banca de análise documental");
        banca.CandidatosAtendidos.Select(static categoria => categoria.Codigo).Should().Equal("RENDA");
        banca.RegraDeDeliberacao!.Quorum.Should().Be(2);
    }

    [Fact(DisplayName = "A banca de análise de recursos com candidatos atendidos é montada como veio: quem a recusa é a fábrica da fase")]
    public async Task Montar_BancaDeRecursosComCandidatosAtendidos_MontaSemRecusar()
    {
        BancaRequeridaInput declarada = new(TipoDeRecursos.Id, [CategoriaRenda.Id], RegrasDeDeliberacaoDeTeste.Valida());

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(declarada), 2, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Single().CandidatosAtendidos.Select(static categoria => categoria.Codigo).Should().Equal("RENDA");
    }

    [Fact(DisplayName = "As violações da regra de deliberação das bancas da fase acumulam sob o caminho de cada uma")]
    public async Task Montar_ComVariasBancasInvalidas_AcumulaTudoSobOCaminhoDeCada()
    {
        FaseCronogramaInput fase = Fase(
            new BancaRequeridaInput(TipoDocumental.Id, [], RegrasDeDeliberacaoDeTeste.ComTresViolacoes()),
            new BancaRequeridaInput(TipoDocumental.Id, [CategoriaRenda.Id], RegrasDeDeliberacaoDeTeste.Valida()));

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(fase, 0, CancellationToken.None);

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

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(desconhecida), 1, CancellationToken.None);

        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("fases[1].bancasRequeridas[0].tipoBancaId");
        recusa.Error.Code.Should().Be("FaseCronograma.TipoBancaNaoEncontrado");
    }

    [Fact(DisplayName = "A categoria ausente do cadastro é recusada no campo dos candidatos atendidos")]
    public async Task Montar_ComCategoriaAusenteDoCadastro_RecusaNoCampoDosCandidatosAtendidos()
    {
        BancaRequeridaInput declarada = new(TipoDocumental.Id, [Guid.CreateVersion7()], null);

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(declarada), 0, CancellationToken.None);

        FieldError recusa = resultado.Errors.Should().ContainSingle().Subject;
        recusa.Field.Should().Be("fases[0].bancasRequeridas[0].categoriasDocumentoIds");
        recusa.Error.Code.Should().Be("FaseCronograma.CategoriaDocumentoNaoEncontrada");
    }

    [Fact(DisplayName = "A banca sem regra é rascunho e a montagem a aceita")]
    public async Task Montar_SemRegra_AceitaORascunho()
    {
        BancaRequeridaInput declarada = new(TipoDocumental.Id, [CategoriaRenda.Id], null);

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(declarada), 0, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Single().RegraDeDeliberacao.Should().BeNull();
    }

    [Fact(DisplayName = "A banca de homologação que decide só a homologação, em ato individual sem votação, é montada")]
    public async Task Montar_HomologacaoEmAtoIndividualSemVotacao_Aceita()
    {
        BancaRequeridaInput declarada = new(TipoDeHomologacao.Id, [], RegrasDeDeliberacaoDeTeste.AtoIndividual());

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(declarada), 0, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Single().RegraDeDeliberacao!.Quorum.Should().BeNull();
    }

    [Fact(DisplayName = "A banca de homologação em ato individual com a votação do recurso inteira é montada com o quórum")]
    public async Task Montar_HomologacaoEmAtoIndividualComVotacaoInteira_Aceita()
    {
        BancaRequeridaInput declarada = new(TipoDeHomologacao.Id, [], RegrasDeDeliberacaoDeTeste.AtoIndividualComVotacaoDoRecurso());

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(declarada), 0, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Single().RegraDeDeliberacao!.QuorumExigido.Should().Be(2);
    }

    [Fact(DisplayName = "A banca de homologação com a votação do recurso pela metade recusa o campo que falta, no caminho da banca")]
    public async Task Montar_HomologacaoEmAtoIndividualComVotacaoPelaMetade_RecusaOsCamposQueFaltam()
    {
        BancaRequeridaInput declarada = new(TipoDeHomologacao.Id, [], RegrasDeDeliberacaoDeTeste.AtoIndividualSoComOQuorum());

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(declarada), 0, CancellationToken.None);

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

        Result<List<BancaRequerida>> resultado = await Montagem().MontarAsync(Fase(declarada), 0, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
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
        Substitute.For<IFatoCandidatoReader>()));

    private static ITipoBancaReader TiposDeBanca()
    {
        ITipoBancaReader leitor = Substitute.For<ITipoBancaReader>();
        leitor.ObterPorIdAsync(TipoDocumental.Id, Arg.Any<CancellationToken>()).Returns(TipoDocumental);
        leitor.ObterPorIdAsync(TipoDeRecursos.Id, Arg.Any<CancellationToken>()).Returns(TipoDeRecursos);
        leitor.ObterPorIdAsync(TipoDeHomologacao.Id, Arg.Any<CancellationToken>()).Returns(TipoDeHomologacao);
        return leitor;
    }

    private static ICategoriaDocumentoReader Categorias()
    {
        ICategoriaDocumentoReader leitor = Substitute.For<ICategoriaDocumentoReader>();
        leitor.ObterPorIdAsync(CategoriaRenda.Id, Arg.Any<CancellationToken>()).Returns(CategoriaRenda);
        return leitor;
    }
}
