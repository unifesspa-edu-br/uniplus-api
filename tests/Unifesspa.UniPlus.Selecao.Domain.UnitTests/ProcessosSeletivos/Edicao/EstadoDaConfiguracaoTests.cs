namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Edicao;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

public sealed class EstadoDaConfiguracaoTests
{
    private static EtapaProcesso Etapa() => EtapaProcesso.Criar(
        "Prova Objetiva",
        CaraterEtapa.Classificatoria,
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), "PROVA_OBJETIVA", "Prova Objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!,
        peso: 1m,
        ordem: 1,
        notaMaxima: 10m,
        formaDeAvaliacao: FormaDeAvaliacao.Objetiva).Value!;

    [Fact(DisplayName = "A vista acompanha a configuração viva do processo")]
    public void De_AcompanhaAConfiguracaoViva()
    {
        ProcessoSeletivo processo = EstadoDaConfiguracaoBuilder.NovoProcesso();
        EstadoDaConfiguracao vista = EstadoDaConfiguracao.De(processo);

        processo.DefinirEtapas([Etapa()], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        vista.ProcessoId.Should().Be(processo.Id);
        vista.Status.Should().Be(StatusProcesso.Rascunho);
        vista.Etapas.Should().HaveCount(1);
    }

    [Fact(DisplayName = "O candidato de uma escrita troca a dimensão na vista sem tocar o processo")]
    public void ComCandidata_NaoAlteraOProcesso()
    {
        ProcessoSeletivo processo = EstadoDaConfiguracaoBuilder.NovoProcesso();

        EstadoDaConfiguracao candidato = EstadoDaConfiguracao.De(processo) with { Etapas = [Etapa()] };

        candidato.Etapas.Should().HaveCount(1);
        processo.Etapas.Should().BeEmpty();
    }

    [Fact(DisplayName = "O builder parte do processo vazio e aceita o que a regra lê")]
    public void Builder_ComposicaoDeclarada()
    {
        EstadoDaConfiguracao estado = new EstadoDaConfiguracaoBuilder().ComEtapas(Etapa()).Publicado().Construir();

        estado.Etapas.Should().HaveCount(1);
        estado.Status.Should().Be(StatusProcesso.Publicado);
    }
}
