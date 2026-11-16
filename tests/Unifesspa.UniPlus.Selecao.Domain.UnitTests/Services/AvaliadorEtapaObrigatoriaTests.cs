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
/// Avaliação de <see cref="EtapaObrigatoria"/>: a regra cita o código do tipo de etapa e a conferência é pela
/// identidade do tipo congelado no processo, nunca pelo rótulo editorial da etapa nem pela string do código.
/// </summary>
public sealed class AvaliadorEtapaObrigatoriaTests
{
    [Fact(DisplayName = "issue #1071 — cenário 1: nome editorial não interfere na conformidade")]
    public void EtapaObrigatoria_ComTipoCongeladoCorrespondente_AprovaIndependenteDoNome()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DefinirEtapa(processo, "Primeira avaliação", NovoTipoEtapa("PROVA_OBJETIVA", "Prova Objetiva"));

        RegraAvaliada avaliada = Avaliar(processo, NovaRegra("ETAPA", new EtapaObrigatoria("PROVA_OBJETIVA")));

        avaliada.Aprovada.Should().BeTrue("o rótulo editorial ('Primeira avaliação') não participa da avaliação — só o código congelado do tipo");
        avaliada.Motivo.Should().BeNull("regra aprovada não carrega motivo de reprovação");
    }

    [Fact(DisplayName = "issue #1071 — cenário 2: nome igual ao código não substitui a identidade do tipo")]
    public void EtapaObrigatoria_MesmoNomeTipoDiferente_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DefinirEtapa(processo, "PROVA_OBJETIVA", NovoTipoEtapa("ENTREVISTA", "Entrevista"));

        RegraAvaliada avaliada = Avaliar(processo, NovaRegra("ETAPA", new EtapaObrigatoria("PROVA_OBJETIVA")));

        avaliada.Aprovada.Should().BeFalse("a etapa se chama 'PROVA_OBJETIVA' mas o tipo congelado é ENTREVISTA — nome não é identidade");
        avaliada.Motivo.Should().Contain("PROVA_OBJETIVA", "a reprovação nomeia o código do tipo ausente, não só um booleano");
    }

    [Fact(DisplayName = "CA (EtapaObrigatoria): reprova nomeando o código do tipo ausente")]
    public void EtapaObrigatoria_SemEtapaCorrespondente_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DefinirEtapa(processo, "Redação", NovoTipoEtapa("REDACAO", "Redação"));

        RegraAvaliada avaliada = Avaliar(processo, NovaRegra("ETAPA", new EtapaObrigatoria("PROVA_OBJETIVA")));

        avaliada.Aprovada.Should().BeFalse();
        avaliada.Motivo.Should().Contain("PROVA_OBJETIVA", "a reprovação nomeia o código do tipo ausente, não só um booleano");
    }

    [Fact(DisplayName = "EtapaObrigatoria: renomear o código do tipo e atualizar a regra continua aprovando")]
    public void EtapaObrigatoria_AposRenomearOCodigoEAtualizarARegra_Aprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        Guid identidadeDoTipo = Guid.CreateVersion7();
        DefinirEtapa(processo, "Prova", NovoTipoEtapa("PROVA_OBJETIVA", "Prova Objetiva", identidadeDoTipo));
        IdentidadesDeCadastro cadastro = IdentidadesDe(processo) with { TiposEtapa = Mapa("PROVA_OBJETIVA_V2", identidadeDoTipo) };

        RegraAvaliada avaliada = Avaliar(processo, NovaRegra("ETAPA", new EtapaObrigatoria("PROVA_OBJETIVA_V2")), cadastro);

        avaliada.Aprovada.Should().BeTrue("a etapa é do mesmo tipo que a regra exige — só o código mudou");
    }

    [Fact(DisplayName = "EtapaObrigatoria: código reatribuído a outro tipo reprova, mesmo com a string batendo")]
    public void EtapaObrigatoria_ComCodigoReatribuido_Reprova()
    {
        ProcessoSeletivo processo = NovoProcesso();
        DefinirEtapa(processo, "Prova", NovoTipoEtapa("PROVA_OBJETIVA", "Prova Objetiva"));
        IdentidadesDeCadastro cadastro = IdentidadesDe(processo) with { TiposEtapa = Mapa("PROVA_OBJETIVA", Guid.CreateVersion7()) };

        RegraAvaliada avaliada = Avaliar(processo, NovaRegra("ETAPA", new EtapaObrigatoria("PROVA_OBJETIVA")), cadastro);

        avaliada.Aprovada.Should().BeFalse();
        avaliada.Motivo.Should().Contain("o código foi reatribuído", "a mensagem diz o que houve, em vez de mandar procurar uma etapa que está na tela");
    }
}
