namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport.ManifestacoesDeTeste;

/// <summary>A situação de cada evento do histórico, derivada na leitura: vigente, substituída ou encerrada.</summary>
public sealed class HistoricoDasManifestacoesTests
{
    private static readonly ItemDaProtecao Item = new(CadastroDoCatalogo.TipoDocumento, Guid.CreateVersion7());
    private static readonly DateTimeOffset Instante = TipoDocumentoNaAprovacaoBuilder.Segunda;
    private static readonly CicloDaAprovacao Segundo = new(2);

    [Fact(DisplayName = "No ciclo corrente a substituída e a vigente se distinguem; o ciclo anterior fica encerrado")]
    public void SituacaoDe_DerivaContraOCicloCorrente()
    {
        ManifestacaoDaProtecaoDoCatalogo encerrada = Evento(CicloDaAprovacao.Primeiro, Kevin, null, 0);
        ManifestacaoDaProtecaoDoCatalogo substituida = Evento(Segundo, Kevin, null, 10);
        ManifestacaoDaProtecaoDoCatalogo vigente = Evento(Segundo, Kevin, substituida, 20);

        HistoricoDasManifestacoes historico = HistoricoDasManifestacoes.De([vigente, encerrada, substituida], Segundo);

        historico.Eventos.Should().Equal(encerrada, substituida, vigente);
        historico.Eventos.Select(historico.SituacaoDe).Should().Equal(
            SituacaoDaManifestacao.Encerrada, SituacaoDaManifestacao.Substituida, SituacaoDaManifestacao.Vigente);
    }

    [Fact(DisplayName = "A substituída de um ciclo encerrado continua substituída")]
    public void SituacaoDe_SubstituidaDeCicloEncerrado()
    {
        ManifestacaoDaProtecaoDoCatalogo substituida = Evento(CicloDaAprovacao.Primeiro, Lucia, null, 0);
        ManifestacaoDaProtecaoDoCatalogo substituta = Evento(CicloDaAprovacao.Primeiro, Lucia, substituida, 5);

        HistoricoDasManifestacoes historico = HistoricoDasManifestacoes.De([substituida, substituta], Segundo);

        historico.SituacaoDe(substituida).Should().Be(SituacaoDaManifestacao.Substituida);
        historico.SituacaoDe(substituta).Should().Be(SituacaoDaManifestacao.Encerrada);
    }

    private static ManifestacaoDaProtecaoDoCatalogo Evento(
        CicloDaAprovacao ciclo, AtorDoAto autor, ManifestacaoDaProtecaoDoCatalogo? substituida, int minutos) =>
        ManifestacaoDaProtecaoDoCatalogo.Registrar(Item, ciclo, Inadequada(), autor, substituida, Instante.AddMinutes(minutos));
}
