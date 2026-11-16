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
/// Avaliação de <see cref="ConcorrenciaDuplaObrigatoria"/> (CA-07): exige modalidade de cota reservada, e a
/// contraprova garante que o predicado não é tautológico.
/// </summary>
public sealed class AvaliadorConcorrenciaDuplaTests
{
    [Fact(DisplayName = "CA-07 (ConcorrenciaDuplaObrigatoria): aprova quando há modalidade CotaReservada")]
    public void ConcorrenciaDupla_ComCotaReservada_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DefinirOfertas(processo, NovaOferta(
            NovaModalidade("AC", NaturezaLegalModalidade.Ampla), NovaModalidade("LB_PPI", NaturezaLegalModalidade.CotaReservada)));

        RegraAvaliada avaliada = Avaliar(processo, NovaRegra("CONCORRENCIA", new ConcorrenciaDuplaObrigatoria()));

        avaliada.Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-07 (ConcorrenciaDuplaObrigatoria — contraprova obrigatória, NÃO é tautológica): REPROVA sem cota reservada")]
    public void ConcorrenciaDupla_SemCotaReservada_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DefinirOfertas(processo, NovaOferta(NovaModalidade("AC", NaturezaLegalModalidade.Ampla)));

        RegraAvaliada avaliada = Avaliar(processo, NovaRegra("CONCORRENCIA", new ConcorrenciaDuplaObrigatoria()));

        avaliada.Aprovada.Should().BeFalse(
            "um processo só de ampla concorrência é legítimo, e a regra só é cadastrada para os tipos em que a lei " +
            "obriga a concorrência dupla — dar este predicado como sempre-aprovado estaria errado");
        avaliada.Motivo.Should().NotBeNullOrWhiteSpace();
    }
}
