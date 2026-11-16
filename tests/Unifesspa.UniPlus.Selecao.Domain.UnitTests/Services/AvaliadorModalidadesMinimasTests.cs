namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services.CenarioDeConformidadeLegal;

/// <summary>
/// Avaliação de <see cref="ModalidadesMinimas"/> (CA-03): toda oferta do processo contém todas as modalidades
/// exigidas, conferidas pela identidade da modalidade e não pela string do código.
/// </summary>
public sealed class AvaliadorModalidadesMinimasTests
{
    [Fact(DisplayName = "CA-03 (ModalidadesMinimas): aprova sse TODA oferta contém todas as modalidades exigidas")]
    public void ModalidadesMinimas_TodasAsOfertasTemAModalidade_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid ampla = Guid.CreateVersion7();
        Guid cota = Guid.CreateVersion7();
        DefinirOfertas(processo, OfertaCompleta(ampla, cota), OfertaCompleta(ampla, cota));

        RegraAvaliada avaliada = Avaliar(processo, NovaRegra("MODALIDADES", new ModalidadesMinimas(["LB_PPI"])));

        avaliada.Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-03 (ModalidadesMinimas — contraprova obrigatória): reprova nomeando a oferta que falhou")]
    public void ModalidadesMinimas_OfertaSemModalidade_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ConfiguracaoDistribuicaoVagas ofertaQueFalha = NovaOferta(NovaModalidade("AC", NaturezaLegalModalidade.Ampla));
        DefinirOfertas(processo, OfertaCompleta(Guid.CreateVersion7(), Guid.CreateVersion7()), ofertaQueFalha);

        RegraAvaliada avaliada = Avaliar(processo, NovaRegra("MODALIDADES", new ModalidadesMinimas(["LB_PPI"])));

        avaliada.Aprovada.Should().BeFalse("a Lei 12.711 reserva vagas por curso e turno — uma oferta sem a modalidade é ilegal ainda que outra a tenha");
        avaliada.Motivo.Should().Contain("LB_PPI").And.Contain(
            ofertaQueFalha.Id.ToString(), "o erro nomeia A OFERTA que falhou, e não a oferta completa");
    }

    [Fact(DisplayName = "ModalidadesMinimas: renomear o código da modalidade e atualizar a regra continua aprovando")]
    public void ModalidadesMinimas_AposRenomearOCodigoEAtualizarARegra_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        ModalidadeSelecionada modalidade = NovaModalidade("LB_PPI", NaturezaLegalModalidade.CotaReservada);
        DefinirOfertas(processo, NovaOferta(modalidade));
        IdentidadesDeCadastro cadastro = IdentidadesDe(processo) with { Modalidades = Mapa("LB_PPI_V2", modalidade.ModalidadeOrigemId) };

        RegraAvaliada avaliada = Avaliar(processo, NovaRegra("MINIMAS", new ModalidadesMinimas(["LB_PPI_V2"])), cadastro);

        avaliada.Aprovada.Should().BeTrue("a oferta contém a mesma modalidade que a regra exige — só o código mudou");
    }

    [Fact(DisplayName = "ModalidadesMinimas: código reatribuído a outra modalidade reprova, mesmo com a string batendo")]
    public void ModalidadesMinimas_ComCodigoReatribuido_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DefinirOfertas(processo, NovaOferta(NovaModalidade("LB_PPI", NaturezaLegalModalidade.CotaReservada)));
        IdentidadesDeCadastro cadastro = IdentidadesDe(processo) with { Modalidades = Mapa("LB_PPI", Guid.CreateVersion7()) };

        RegraAvaliada avaliada = Avaliar(processo, NovaRegra("MINIMAS", new ModalidadesMinimas(["LB_PPI"])), cadastro);

        avaliada.Aprovada.Should().BeFalse("a modalidade ofertada com esse código é outra — a exigência mínima não está cumprida");
        avaliada.Motivo.Should().Contain("o código foi reatribuído");
    }

    private static ConfiguracaoDistribuicaoVagas OfertaCompleta(Guid ampla, Guid cota) =>
        NovaOferta(
            NovaModalidade("AC", NaturezaLegalModalidade.Ampla, ampla),
            NovaModalidade("LB_PPI", NaturezaLegalModalidade.CotaReservada, cota));
}
