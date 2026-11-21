namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.PublicacaoDeTeste;

/// <summary>
/// O instante que o comando lê do relógio uma só vez, e o instante efetivo de quem sucede uma versão: o maior entre o
/// relógio e a vigência dela, com o dia civil do instante lido.
/// </summary>
public sealed class LeituraDoInstanteDoComandoTests
{
    private static readonly ResolvedorFusoDeTeste Fuso = new();

    [Fact(DisplayName = "Ler devolve o instante do relógio e o dia civil dele no fuso institucional")]
    public void Ler_DevolveORelogioEODiaCivilDele()
    {
        LeituraDoInstanteDoComando leitura = LeituraComRelogioEm(Agora);

        Result<InstanteDoComando> lido = leitura.Ler();

        lido.Value.Instante.Should().Be(Agora);
        lido.Value.Dia.Should().Be(new DateOnly(2026, 1, 1));
    }

    [Fact(DisplayName = "Quem sucede uma versão com o relógio adiantado em relação à vigência dela lê o instante do relógio")]
    public void LerNaSucessaoDe_RelogioAFrenteDaVigencia_DevolveORelogio()
    {
        (_, VersaoConfiguracao anterior) = ProcessoPublicado();
        LeituraDoInstanteDoComando leitura = LeituraComRelogioEm(anterior.VigenteAPartirDe.AddMinutes(30));

        Result<InstanteDoComando> lido = leitura.LerNaSucessaoDe(anterior);

        lido.Value.Instante.Should().Be(anterior.VigenteAPartirDe.AddMinutes(30));
    }

    [Fact(DisplayName = "Quem sucede uma versão com o relógio no instante da vigência dela lê esse mesmo instante")]
    public void LerNaSucessaoDe_RelogioNoInstanteDaVigencia_DevolveOMesmoInstante()
    {
        (_, VersaoConfiguracao anterior) = ProcessoPublicado();
        LeituraDoInstanteDoComando leitura = LeituraComRelogioEm(anterior.VigenteAPartirDe);

        Result<InstanteDoComando> lido = leitura.LerNaSucessaoDe(anterior);

        lido.Value.Instante.Should().Be(anterior.VigenteAPartirDe);
    }

    [Fact(DisplayName = "Quem sucede uma versão com o relógio atrás da vigência dela lê a vigência, e o dia civil é o desse instante")]
    public void LerNaSucessaoDe_RelogioAtrasDaVigencia_DevolveAVigenciaEODiaDela()
    {
        (_, VersaoConfiguracao anterior) = ProcessoPublicado();
        DateTimeOffset relogioNoDiaCivilAnterior = anterior.VigenteAPartirDe.AddHours(-10);
        LeituraDoInstanteDoComando leitura = LeituraComRelogioEm(relogioNoDiaCivilAnterior);

        Result<InstanteDoComando> lido = leitura.LerNaSucessaoDe(anterior);

        lido.Value.Instante.Should().Be(anterior.VigenteAPartirDe);
        lido.Value.Dia.Should().Be(new DateOnly(2026, 1, 1), "o relógio regressivo cairia no dia civil anterior, e o tipo do ato vigora no dia do instante efetivo");
    }

    [Fact(DisplayName = "Sem a versão que sucede, a leitura é recusada como erro de programação")]
    public void LerNaSucessaoDe_SemVersaoAnterior_Lanca()
    {
        LeituraDoInstanteDoComando leitura = LeituraComRelogioEm(Agora);

        Action semVersao = () => leitura.LerNaSucessaoDe(null!);

        semVersao.Should().Throw<ArgumentNullException>();
    }

    private static LeituraDoInstanteDoComando LeituraComRelogioEm(DateTimeOffset instante) =>
        new(new RelogioFixo(instante), Fuso);
}
