namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Leitura pública do certame, detalhe e vitrine, sobre a tabela de divulgações.
/// </summary>
/// <remarks>
/// A existência da linha É a publicidade, e é isso que estes testes exercitam: não há critério de
/// visibilidade a aplicar na leitura, porque quem não é público não tem linha.
/// </remarks>
public sealed class LeituraPublicaDoCertameTests
{
    private static readonly DateTimeOffset Agora = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "Certame sem divulgação responde não encontrado")]
    public async Task Detalhe_SemDivulgacao_NaoEncontrado()
    {
        // Inexistente, rascunho, sem versão vigente e com ato não confirmado caem aqui pelo mesmo
        // caminho: nenhum deles tem linha. Distinguir deixou de ser possível, em vez de ser
        // possível e proibido.
        ICertameDivulgadoRepository repository = Substitute.For<ICertameDivulgadoRepository>();
        repository.ObterParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((CertameDivulgado?)null);

        Result<CertamePublicadoDto> resultado = await ObterCertamePublicadoQueryHandler.Handle(
            new ObterCertamePublicadoQuery(Guid.CreateVersion7()), repository, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Certame divulgado devolve a projeção que foi materializada")]
    public async Task Detalhe_ComDivulgacao_DevolveAProjecao()
    {
        Guid processoId = Guid.CreateVersion7();
        CertamePublicadoDto esperado = Projecao(processoId);
        ICertameDivulgadoRepository repository = Substitute.For<ICertameDivulgadoRepository>();
        repository.ObterParaLeituraAsync(processoId, Arg.Any<CancellationToken>())
            .Returns(Divulgado(processoId, esperado));

        Result<CertamePublicadoDto> resultado = await ObterCertamePublicadoQueryHandler.Handle(
            new ObterCertamePublicadoQuery(processoId), repository, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.AtoCriadorId.Should().Be(esperado.AtoCriadorId);
        resultado.Value.Periodo.Numero.Should().Be("001/2026");
        resultado.Value.Nome.Should().Be("SISU 2026.1", "o título é congelado na divulgação, não lido do agregado vivo");
    }

    [Fact(DisplayName = "A vitrine devolve a página inteira: não há descarte depois de formada")]
    public async Task Handle_QuandoHaVariosDivulgados_DeveDevolverAPaginaInteira()
    {
        Guid a = Guid.CreateVersion7();
        Guid b = Guid.CreateVersion7();
        ICertameDivulgadoRepository repository = RepositorioComVitrine(a, b);

        ListarCertamesPublicadosResult resultado = (await ListarCertamesPublicadosQueryHandler.Handle(
            Consulta(), repository, CancellationToken.None)).Value!;

        resultado.Items.Should().HaveCount(2, "só há linha para certame público, então nada é filtrado depois");
        resultado.Items.Select(static i => i.ProcessoSeletivoId).Should().ContainInOrder(a, b);
    }

    [Theory(DisplayName = "A situação do item é resolvida no servidor, contra o instante da navegação")]
    [InlineData(5, 30, SituacaoDoCertame.EmBreve)]
    [InlineData(-1, 30, SituacaoDoCertame.InscricoesAbertas)]
    [InlineData(-1, 3, SituacaoDoCertame.UltimosDias)]
    [InlineData(-30, -1, SituacaoDoCertame.Encerradas)]
    public async Task Handle_QuandoJanelaEmCadaPonto_DeveMarcarASituacaoDoInstante(int diasAteAbrir, int diasAteFechar, SituacaoDoCertame esperada)
    {
        // O fuso de quem lê não decide prazo de edital, e a janela tem dois lados: um edital
        // publicado antes de a inscrição abrir não está recebendo inscrição.
        Guid processoId = Guid.CreateVersion7();
        ICertameDivulgadoRepository repository = Substitute.For<ICertameDivulgadoRepository>();
        CertameDivulgado linha = Divulgado(
            processoId, Projecao(processoId),
            inscricoesDe: Agora.AddDays(diasAteAbrir), inscricoesAte: Agora.AddDays(diasAteFechar));
        repository.ListarVitrineAsync(
                Arg.Any<DateTimeOffset>(), Arg.Any<RecorteDaVitrine>(), Arg.Any<IReadOnlyList<SortField>>(),
                Arg.Any<TimeSpan>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<int>(),
                Arg.Any<PaginationDirection>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new PaginaDaVitrine([linha], Agora, null, null, null));

        ListarCertamesPublicadosResult resultado = (await ListarCertamesPublicadosQueryHandler.Handle(
            Consulta(), repository, CancellationToken.None)).Value!;

        resultado.Items.Should().ContainSingle().Which.Situacao.Should().Be(esperada);
    }

    [Fact(DisplayName = "Contadores só são calculados quando pedidos")]
    public async Task Handle_QuandoNaoPedeContadores_DeveOmitiLos()
    {
        // Contar é percurso a mais sobre a coleção inteira, e o cursor preserva o parâmetro: quem
        // pede uma vez pagaria em toda página. Quem decide é a consulta, e o repositório responde
        // ao que ela pediu — por isso o dublê devolve os números só quando o sinalizador chega.
        ICertameDivulgadoRepository repository = RepositorioComVitrine(Guid.CreateVersion7());

        ListarCertamesPublicadosResult sem = (await ListarCertamesPublicadosQueryHandler.Handle(
            Consulta(), repository, CancellationToken.None)).Value!;
        ListarCertamesPublicadosResult com = (await ListarCertamesPublicadosQueryHandler.Handle(
            Consulta(incluirContadores: true), repository, CancellationToken.None)).Value!;

        sem.Contadores.Should().BeNull();
        com.Contadores.Should().Be(new ContadoresDaVitrine(5, 12, 3, 40));
    }

    /// <summary>Posição de <c>incluirContadores</c> na chamada ao repositório.</summary>
    private const int PosicaoDoSinalizadorDeContadores = 8;

    private static ListarCertamesPublicadosQuery Consulta(bool incluirContadores = false) =>
        new(Agora, new RecorteDaVitrine(), [], null, null, 20, PaginationDirection.Next, incluirContadores);

    private static ICertameDivulgadoRepository RepositorioComVitrine(params Guid[] processoIds)
    {
        ICertameDivulgadoRepository repository = Substitute.For<ICertameDivulgadoRepository>();
        CertameDivulgado[] linhas = [.. processoIds.Select(id => Divulgado(id, Projecao(id)))];
        repository.ListarVitrineAsync(
                Arg.Any<DateTimeOffset>(), Arg.Any<RecorteDaVitrine>(), Arg.Any<IReadOnlyList<SortField>>(),
                Arg.Any<TimeSpan>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<int>(),
                Arg.Any<PaginationDirection>(), Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(chamada => new PaginaDaVitrine(
                linhas,
                Agora,
                null,
                ("ancora", processoIds[^1]),
                chamada.ArgAt<bool>(PosicaoDoSinalizadorDeContadores) ? new ContadoresDaVitrine(5, 12, 3, 40) : null));
        return repository;
    }

    private static CertameDivulgado Divulgado(
        Guid processoId,
        CertamePublicadoDto projecao,
        DateTimeOffset? inscricoesDe = null,
        DateTimeOffset? inscricoesAte = null) =>
        CertameDivulgado.Criar(
            processoId,
            numeroVersao: 1,
            projecao.AtoCriadorId,
            new string('a', 64),
            ProjecaoDoCertamePublicado.Versao,
            new FacetasDoCertameDivulgado(
                projecao.IdentificadorLegivel,
                projecao.Nome,
                projecao.Periodo.Numero,
                projecao.ModalidadesOfertadas,
                inscricoesDe ?? Agora.AddDays(-1),
                inscricoesAte ?? Agora.AddDays(20)),
            JsonSerializer.Serialize(projecao, ProjecaoDoCertamePublicado.OpcoesDoDocumento),
            Agora);

    private static CertamePublicadoDto Projecao(Guid processoId) => new(
        processoId,
        IdentificadoresDeTeste.Novo().Valor,
        Guid.CreateVersion7(),
        "SISU 2026.1",
        ProjecaoDoCertamePublicado.Versao,
        new string('a', 64),
        new TipoCatalogadoCertameDto("SISU", "Sistema de Seleção Unificada"),
        new PeriodoInscricaoCertameDto("001/2026", Agora.AddDays(-1), Agora.AddDays(20)),
        new LocalidadeCertameDto("1504208", "Marabá", "PA", "America/Belem"),
        new UnidadeAdministradoraCertameDto("UNIFESSPA", "Universidade", "Autarquia", "Marabá", "PA"),
        new DocumentoEditalCertameDto(Guid.CreateVersion7(), new string('b', 64)),
        [],
        ["AC"],
        [new QuadroDeVagasCertameDto(Guid.CreateVersion7(), [], 100)],
        [],
        "Externa",
        [],
        [],
        new AtendimentoCertameDto([], [], []),
        null,
        null);
}
