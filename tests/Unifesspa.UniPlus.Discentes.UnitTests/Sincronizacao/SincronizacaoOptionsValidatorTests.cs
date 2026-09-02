namespace Unifesspa.UniPlus.Discentes.UnitTests.Sincronizacao;

using AwesomeAssertions;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

public sealed class SincronizacaoOptionsValidatorTests
{
    [Fact]
    public void Aceita_a_configuracao_padrao()
    {
        Validar(new SincronizacaoOptions()).Succeeded.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Recusa_lote_nao_positivo(int tamanho)
    {
        // Negativo derruba a execução depois de a marca dela já ter sido criada; zero faz
        // cada vínculo virar uma transação, degradando a sincronização sem nada acusar.
        ValidateOptionsResult resultado = Validar(new SincronizacaoOptions { TamanhoDoLote = tamanho });

        resultado.Failed.Should().BeTrue();
        resultado.FailureMessage.Should().Contain("TamanhoDoLote");
    }

    [Fact]
    public void Recusa_janela_de_ingresso_vazia()
    {
        Validar(new SincronizacaoOptions { AnosDeIngressoConsiderados = 0 })
            .Failed.Should().BeTrue();
    }

    [Fact]
    public void Recusa_lista_de_situacoes_vazia()
    {
        // Sem ela, vínculos em andamento com ingresso antigo deixariam de ser alcançados.
        ValidateOptionsResult resultado = Validar(new SincronizacaoOptions { SituacoesEmAndamento = [] });

        resultado.Failed.Should().BeTrue();
        resultado.FailureMessage.Should().Contain("SituacoesEmAndamento");
    }

    [Fact]
    public void Recusa_nivel_de_ensino_vazio()
    {
        Validar(new SincronizacaoOptions { NivelDeEnsino = "" }).Failed.Should().BeTrue();
    }

    private static ValidateOptionsResult Validar(SincronizacaoOptions opcoes) =>
        new SincronizacaoOptionsValidator().Validate(name: null, opcoes);
}
