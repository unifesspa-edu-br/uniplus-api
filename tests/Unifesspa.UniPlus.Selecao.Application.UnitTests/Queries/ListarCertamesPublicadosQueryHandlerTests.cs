namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using System.Text;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Cobertura da vitrine pública: o que entra na página, o que é descartado, e por quê o descarte
/// não corrompe a navegação.
/// </summary>
public sealed class ListarCertamesPublicadosQueryHandlerTests
{
    private const string VersaoReconhecida = "1.0";
    private static readonly DateTimeOffset Agora = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

    private const string Envelope = """
        {
          "tipoProcesso": {"origemId": "0199a1b2-1111-7000-8000-000000000001", "codigo": "SISU", "nome": "Sistema de Seleção Unificada"},
          "periodo": {"numero": "001/2026", "inicio": "2026-03-01T03:00:00Z", "fim": "2026-03-20T02:59:59Z"},
          "modalidadesOfertadas": ["AC", "LB_PPI"],
          "vagas": [
            {"ofertaCursoOrigemId": "0199a1b2-2222-7000-8000-000000000002", "quadro": [], "totalPublicado": 40},
            {"ofertaCursoOrigemId": "0199a1b2-3333-7000-8000-000000000003", "quadro": [], "totalPublicado": 60}
          ]
        }
        """;

    [Fact(DisplayName = "Certame com ato registrado entra na página, com o total de vagas somado por oferta")]
    public async Task Handle_AtoRegistrado_EntraNaPagina()
    {
        Guid processoId = Guid.CreateVersion7();
        Guid ato = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = RepositorioCom((processoId, "SISU 2026.1", ato));

        ListarCertamesPublicadosResult resultado = await HandleAsync(repository, LeitorCom(ato));

        resultado.Items.Should().ContainSingle();
        CertameNaVitrineDto item = resultado.Items[0];
        item.ProcessoSeletivoId.Should().Be(processoId);
        item.Nome.Should().Be("SISU 2026.1");
        item.Numero.Should().Be("001/2026");
        item.TotalDeVagas.Should().Be(100, "o total soma as ofertas, não o quadro por modalidade");
        item.ModalidadesOfertadas.Should().BeEquivalentTo(["AC", "LB_PPI"]);
    }

    [Fact(DisplayName = "Candidato sem ato registrado é descartado, e a página fica menor que o limite")]
    public async Task Handle_SemAtoRegistrado_DescartaSemDerrubarAPagina()
    {
        // O descarte acontece DEPOIS de a página ser formada: a ordenação e o corte são do banco, e
        // o ato vive noutro módulo. Quem navega segue a âncora, nunca conclui fim por página curta.
        Guid comAto = Guid.CreateVersion7();
        Guid semAto = Guid.CreateVersion7();
        Guid atoRegistrado = Guid.CreateVersion7();

        IProcessoSeletivoRepository repository = RepositorioCom(
            (comAto, "Com ato", atoRegistrado),
            (semAto, "Sem ato", Guid.CreateVersion7()));

        ListarCertamesPublicadosResult resultado = await HandleAsync(repository, LeitorCom(atoRegistrado));

        resultado.Items.Should().ContainSingle().Which.ProcessoSeletivoId.Should().Be(comAto);
        resultado.Proximo.Should().NotBeNull("a âncora vem do candidato considerado, não do item devolvido");
    }

    [Fact(DisplayName = "Nenhum candidato com ato devolve página vazia, preservando a continuação")]
    public async Task Handle_NenhumAtoRegistrado_PaginaVaziaComAncora()
    {
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = RepositorioCom((processoId, "Sem ato", Guid.CreateVersion7()));

        ListarCertamesPublicadosResult resultado = await HandleAsync(repository, LeitorCom());

        resultado.Items.Should().BeEmpty();
        resultado.Proximo.Should().NotBeNull("página vazia não significa fim de coleção");
    }

    [Fact(DisplayName = "A situação das inscrições é resolvida no servidor, contra o instante da navegação")]
    public async Task Handle_PrazoVencido_MarcaInscricoesFechadas()
    {
        // O fuso de quem lê não decide prazo de edital: o servidor compara contra o instante que a
        // navegação congelou e devolve a resposta pronta.
        Guid processoId = Guid.CreateVersion7();
        Guid ato = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = RepositorioCom(
            Agora.AddDays(-1), (processoId, "Encerrado", ato));

        ListarCertamesPublicadosResult resultado = await HandleAsync(repository, LeitorCom(ato));

        resultado.Items.Should().ContainSingle().Which.InscricoesAbertas.Should().BeFalse();
    }

    private static Task<ListarCertamesPublicadosResult> HandleAsync(
        IProcessoSeletivoRepository repository,
        IAtoRegistradoReader leitor) =>
        ListarCertamesPublicadosQueryHandler.Handle(
            new ListarCertamesPublicadosQuery(Agora, SituacaoDoCertame.Todas, null, null, 20, PaginationDirection.Next),
            repository,
            leitor,
            RegistroReconhecendo(VersaoReconhecida),
            CancellationToken.None);

    private static IProcessoSeletivoRepository RepositorioCom(
        params (Guid ProcessoId, string Nome, Guid AtoCriadorId)[] certames) =>
        RepositorioCom(Agora.AddDays(5), certames);

    private static IProcessoSeletivoRepository RepositorioCom(
        DateTimeOffset prazo,
        params (Guid ProcessoId, string Nome, Guid AtoCriadorId)[] certames)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();

        CandidatoDaVitrine[] candidatos =
            [.. certames.Select(c => new CandidatoDaVitrine(c.ProcessoId, c.Nome, prazo))];

        repository.ListarVitrineAsync(
                Arg.Any<DateTimeOffset>(), Arg.Any<SituacaoDoCertame>(), Arg.Any<string?>(), Arg.Any<Guid?>(),
                Arg.Any<int>(), Arg.Any<PaginationDirection>(), Arg.Any<CancellationToken>())
            .Returns((candidatos, Agora, ((string, Guid)?)null, ("ancora", certames[^1].ProcessoId)));

        repository.ObterLinhagensVigentesAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(certames.ToDictionary(
                c => c.ProcessoId,
                c => (IReadOnlyList<LinhagemDeVersao>)[new LinhagemDeVersao(1, c.AtoCriadorId)])
                as IReadOnlyDictionary<Guid, IReadOnlyList<LinhagemDeVersao>>);

        repository.ObterVersoesPorNumeroAsync(Arg.Any<IReadOnlyCollection<LinhagemDeVersaoDeProcesso>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => (IReadOnlyList<VersaoConfiguracao>)
                [.. callInfo.Arg<IReadOnlyCollection<LinhagemDeVersaoDeProcesso>>()
                    .Select(eleita => VersaoConfiguracao.Abrir(
                        eleita.ProcessoSeletivoId,
                        Encoding.UTF8.GetBytes(Envelope),
                        VersaoReconhecida,
                        "canonical-json/sha256@v1",
                        certames.First(c => c.ProcessoId == eleita.ProcessoSeletivoId).AtoCriadorId,
                        new string('a', 64),
                        "user-sub-123",
                        Agora))]);

        return repository;
    }

    private static IAtoRegistradoReader LeitorCom(params Guid[] registrados)
    {
        IAtoRegistradoReader leitor = Substitute.For<IAtoRegistradoReader>();
        leitor.FiltrarRegistradosAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => (IReadOnlySet<Guid>)new HashSet<Guid>(
                callInfo.Arg<IReadOnlyCollection<Guid>>().Where(registrados.Contains)));
        return leitor;
    }

    private static IRegistroCodecsEnvelope RegistroReconhecendo(params string[] versoes)
    {
        IRegistroCodecsEnvelope registro = Substitute.For<IRegistroCodecsEnvelope>();
        registro.Capacidades.Returns(versoes
            .Select(static v => new CapacidadeCodec(v, TemEncoder: true, TemDecoder: true, MotivoDaRecusa: null))
            .ToList());
        return registro;
    }
}
