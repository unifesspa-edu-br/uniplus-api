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
/// Avaliação de <see cref="AtendimentoDisponivel"/> (CA-06): a regra cita o código do tipo de deficiência e a
/// conferência é pela identidade do tipo congelado, nunca pelo nome exibido nem pela string do código.
/// </summary>
public sealed class AvaliadorAtendimentoDisponivelTests
{
    [Fact(DisplayName = "CA-06 (AtendimentoDisponivel): aprova quando todo código exigido está entre os tipos ofertados")]
    public void AtendimentoDisponivel_ComNecessidadeOfertada_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DefinirAtendimento(processo, Guid.CreateVersion7(), "AUDITIVA", "auditiva");

        RegraAvaliada avaliada = Avaliar(processo, NovaRegra("ATENDIMENTO", new AtendimentoDisponivel(["AUDITIVA"])));

        avaliada.Aprovada.Should().BeTrue();
    }

    [Fact(DisplayName = "CA-06 (AtendimentoDisponivel): reprova SEM lançar quando AtendimentoEspecializado é nula")]
    public void AtendimentoDisponivel_OfertaNula_ReprovaSemLancar()
    {
        ObrigatoriedadeLegal regra = NovaRegra("ATENDIMENTO", new AtendimentoDisponivel(["AUDITIVA"]));

        RegraAvaliada? avaliada = null;
        Action avaliar = () => avaliada = Avaliar(NovoProcesso(), regra, IdentidadesDeCadastro.Vazio);

        avaliar.Should().NotThrow();
        avaliada!.Aprovada.Should().BeFalse();
        avaliada.Motivo.Should().NotBeNullOrWhiteSpace();
    }

    [Fact(DisplayName = "AtendimentoDisponivel: renomear o tipo de deficiência não quebra a regra escrita antes")]
    public void AtendimentoDisponivel_AposRenomearOTipo_ContinuaAprovando()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DefinirAtendimento(processo, Guid.CreateVersion7(), "AUDITIVA", "Nome completamente diferente do que a regra cita");

        RegraAvaliada avaliada = Avaliar(processo, NovaRegra("ATENDIMENTO", new AtendimentoDisponivel(["AUDITIVA"])));

        avaliada.Aprovada.Should().BeTrue("a regra cita o código, e o código não mudou — só o rótulo de exibição");
    }

    [Fact(DisplayName = "AtendimentoDisponivel: o nome ofertado não satisfaz a regra que cita o código")]
    public void AtendimentoDisponivel_ComNomeNoLugarDoCodigo_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DefinirAtendimento(processo, Guid.CreateVersion7(), "DEFICIENCIA_AUDITIVA", "AUDITIVA");

        RegraAvaliada avaliada = Avaliar(processo, NovaRegra("ATENDIMENTO", new AtendimentoDisponivel(["AUDITIVA"])));

        avaliada.Aprovada.Should().BeFalse("o tipo ofertado tem código DEFICIENCIA_AUDITIVA; AUDITIVA é apenas o nome dele");
    }

    [Fact(DisplayName = "AtendimentoDisponivel: renomear o código do tipo e atualizar a regra continua aprovando")]
    public void AtendimentoDisponivel_AposRenomearOCodigoEAtualizarARegra_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid identidadeDoTipo = Guid.CreateVersion7();
        DefinirAtendimento(processo, identidadeDoTipo, "AUDITIVA", "Deficiência auditiva");
        IdentidadesDeCadastro cadastro = IdentidadesDe(processo) with { TiposDeficiencia = Mapa("DEF_AUDITIVA", identidadeDoTipo) };

        RegraAvaliada avaliada = Avaliar(processo, NovaRegra("ATENDIMENTO", new AtendimentoDisponivel(["DEF_AUDITIVA"])), cadastro);

        avaliada.Aprovada.Should().BeTrue("a oferta é do mesmo tipo de deficiência que a regra exige — só o código mudou");
    }

    [Fact(DisplayName = "AtendimentoDisponivel: código reatribuído a outro tipo reprova, mesmo com a string batendo")]
    public void AtendimentoDisponivel_ComCodigoReatribuido_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DefinirAtendimento(processo, Guid.CreateVersion7(), "AUDITIVA", "Deficiência auditiva");
        IdentidadesDeCadastro cadastro = IdentidadesDe(processo) with { TiposDeficiencia = Mapa("AUDITIVA", Guid.CreateVersion7()) };

        RegraAvaliada avaliada = Avaliar(processo, NovaRegra("ATENDIMENTO", new AtendimentoDisponivel(["AUDITIVA"])), cadastro);

        avaliada.Aprovada.Should().BeFalse("o atendimento que carrega o código pertence a outro tipo de deficiência");
        avaliada.Motivo.Should().Contain("o código foi reatribuído");
    }
}
