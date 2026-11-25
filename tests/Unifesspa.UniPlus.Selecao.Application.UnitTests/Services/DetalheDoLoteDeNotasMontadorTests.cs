namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.NotasDoEnem;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class DetalheDoLoteDeNotasMontadorTests
{
    [Fact(DisplayName = "Cada inscrito recebe o nome congelado na sua versão")]
    public async Task MontarAsync_DuasVersoes_UsaNomesCongelados()
    {
        Guid processoId = Guid.CreateVersion7(), ofertaId = Guid.CreateVersion7();
        Guid inscricaoA = Guid.CreateVersion7(), inscricaoB = Guid.CreateVersion7();
        VersaoConfiguracao versaoA = DivulgacaoDeTeste.VersaoSemReferenciaTemporal(processoId);
        VersaoConfiguracao versaoB = DivulgacaoDeTeste.VersaoSemReferenciaTemporal(processoId);
        IProcessoSeletivoRepository processos = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope codecs = Substitute.For<IRegistroCodecsEnvelope>();
        ConfigurarVersoes(processos, codecs, versaoA, versaoB, ofertaId);
        ILoteDeNotasDoEnemRepository lotes = Lotes(inscricaoA, inscricaoB, versaoA.Id, versaoB.Id, ofertaId);
        LoteDeNotasDoEnemDto resultado = await new DetalheDoLoteDeNotasMontador(lotes, new ContextoDaVersaoFixadaResolver(processos, codecs))
            .MontarAsync(Lote(processoId, versaoA.Id), true, CancellationToken.None);
        ConferirOfertas(resultado, inscricaoA, inscricaoB, ofertaId);
        codecs.Received(1).Reidratar(versaoA);
        codecs.Received(1).Reidratar(versaoB);
    }

    private static void ConferirOfertas(LoteDeNotasDoEnemDto resultado, Guid inscricaoA, Guid inscricaoB, Guid ofertaId)
    {
        IReadOnlyList<AusenteDoLoteDto> ausentes = resultado.Ausentes ?? throw new InvalidOperationException("Ausentes não foram montados.");
        ausentes.Single(a => a.InscricaoId == inscricaoA).Ofertas.Should().Equal(new OfertaEscolhidaDto(ofertaId, "Curso A (Bacharelado) — Campus Universitário de Marabá — Matutino", 1));
        ausentes.Single(a => a.InscricaoId == inscricaoB).Ofertas.Should().Equal(new OfertaEscolhidaDto(ofertaId, "Curso B (Bacharelado) — Campus Universitário de Marabá — Matutino", 1));
    }

    private static void ConfigurarVersoes(IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs,
        VersaoConfiguracao versaoA, VersaoConfiguracao versaoB, Guid ofertaId)
    {
        (_, ConfiguracaoPublicada configuracao) = DivulgacaoDeTeste.VersaoConforme(versaoA.ProcessoSeletivoId);
        DivulgacaoDeTeste.ConfigurarDivulgada(processos, codecs, versaoA,
            OfertasDeTeste.ComDistribuicao(configuracao, OfertasDeTeste.DistribuicaoDoCurso(ofertaId, "Curso A", "AC")));
        DivulgacaoDeTeste.ConfigurarDivulgada(processos, codecs, versaoB,
            OfertasDeTeste.ComDistribuicao(configuracao, OfertasDeTeste.DistribuicaoDoCurso(ofertaId, "Curso B", "AC")));
        processos.ObterVersoesAsync(versaoA.ProcessoSeletivoId, Arg.Any<CancellationToken>()).Returns([versaoA, versaoB]);
    }

    private static ILoteDeNotasDoEnemRepository Lotes(Guid a, Guid b, Guid versaoA, Guid versaoB, Guid oferta)
    {
        ILoteDeNotasDoEnemRepository lotes = Substitute.For<ILoteDeNotasDoEnemRepository>();
        lotes.ObterAusentesDoLoteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, IReadOnlyList<string>> { [a] = ["LC"], [b] = ["LC"] });
        lotes.ObterInscritosAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, InscritoDoLote> { [a] = new("1", "Pessoa A", "", versaoA, oferta, null), [b] = new("2", "Pessoa B", "", versaoB, oferta, null) });
        return lotes;
    }

    private static LoteDeNotasDoEnem Lote(Guid processo, Guid versao) => LoteDeNotasDoEnem.Registrar(
        processo, Guid.CreateVersion7(), "2025", "INEP_ENEM_RETORNO", "v1", new string('a', 64), new string('b', 64),
        "conta", new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), 0, versao, new ResultadoDoCruzamento([], [], [], []), []);
}
