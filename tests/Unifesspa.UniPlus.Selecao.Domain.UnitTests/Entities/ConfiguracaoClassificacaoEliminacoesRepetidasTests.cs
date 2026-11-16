namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaConfiguracaoClassificacao;

public sealed class ConfiguracaoClassificacaoEliminacoesRepetidasTests
{
    [Fact(DisplayName = "Cada repetição da falta em dia de prova do ENEM é recusada no próprio item")]
    public void Criar_FaltaEmDiaDeProvaEnemRepetida_Recusa()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoEnemLocal() with
        {
            RegrasEliminacao = Eliminacoes(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, RegraEliminacaoCodigo.ElimZeroEmArea, RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem),
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors
            .Where(e => e.Error.Code == "ConfiguracaoClassificacao.FaltaEmDiaDeProvaEnemRepetida")
            .Select(e => e.Field)
            .Should().Equal(["regrasEliminacao[2]", "regrasEliminacao[3]"], "a primeira declaração vale; cada repetição é recusada no próprio item");
    }

    [Fact(DisplayName = "Na classificação importada, a falta em dia de prova repetida só leva a recusa da lista inteira")]
    public void Criar_ImportadaComFaltaEmDiaDeProvaEnemRepetida_RecusaSoAListaInteira()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoImportada() with
        {
            RegrasEliminacao = Eliminacoes(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem),
            BaseadoEmEnem = true,
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            [("regrasEliminacao", "ConfiguracaoClassificacao.EliminacaoIndevida")],
            "a importada não admite eliminação nenhuma, e é isso que o operador precisa corrigir");
    }

    [Fact(DisplayName = "Sem ENEM, a falta em dia de prova repetida sai junto com a recusa do ENEM desmarcado")]
    public void Criar_SemEnemComFaltaEmDiaDeProvaEnemRepetida_AcumulaAsDuas()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoMediaPonderada() with
        {
            RegrasEliminacao = Eliminacoes(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem),
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            [
                ("regrasEliminacao", "ProcessoSeletivo.EliminacaoEnemForaDeProcessoEnem"),
                ("regrasEliminacao[1]", "ConfiguracaoClassificacao.FaltaEmDiaDeProvaEnemRepetida"),
            ],
            "marcar o ENEM corrige a primeira recusa, e a repetição continua lá: são violações independentes");
    }

    [Fact(DisplayName = "Falta em dia de prova do ENEM declarada uma vez, junto de outra regra ENEM, é aceita")]
    public void Criar_FaltaEmDiaDeProvaEnemUnicaComZeroEmArea_Sucesso()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoEnemLocal() with
        {
            RegrasEliminacao = Eliminacoes(RegraEliminacaoCodigo.ElimFaltaEmDiaDeProvaEnem, RegraEliminacaoCodigo.ElimZeroEmArea),
        });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "A eliminação por ausência repetida para a mesma etapa é recusada no próprio item; etapas distintas valem")]
    public void Criar_AusenciaNaMesmaEtapaRepetida_Recusa()
    {
        Guid etapa = Guid.CreateVersion7();

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoMediaPonderada() with
        {
            RegrasEliminacao = [AusenciaNaEtapa(etapa), AusenciaNaEtapa(Guid.CreateVersion7()), AusenciaNaEtapa(etapa)],
        });

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().Equal(
            [("regrasEliminacao[2]", "ConfiguracaoClassificacao.AusenciaNaEtapaRepetida")]);
    }
}
