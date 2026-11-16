namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaConfiguracaoClassificacao;

public sealed class ConfiguracaoClassificacaoCortesTests
{
    [Fact(DisplayName = "Cortes em áreas diferentes são aceitos")]
    public void Criar_CortesEmAreasDiferentes_Aceita()
    {
        Result<ConfiguracaoClassificacao> resultado = CriarEnemLocal(CorteEm("REDACAO"), CorteEm("MATEMATICA", 450m));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.RegrasEliminacao.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Cada corte repetido na mesma área é recusado no próprio item")]
    public void Criar_CorteRepetidoNaMesmaArea_Recusa()
    {
        Result<ConfiguracaoClassificacao> resultado = CriarEnemLocal(
            CorteEm("REDACAO"), CorteEm("MATEMATICA"), CorteEm("REDACAO", 500m), CorteEm("REDACAO", 300m));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("regrasEliminacao[2]", "ConfiguracaoClassificacao.CorteEmAreaRepetido"),
            ("regrasEliminacao[3]", "ConfiguracaoClassificacao.CorteEmAreaRepetido"),
        ]);
    }

    [Fact(DisplayName = "Corte em área que o quadro de pesos por área não tem é recusado na área do item, com as áreas aceitas")]
    public void Criar_CorteForaDoQuadro_Recusa()
    {
        Result<ConfiguracaoClassificacao> resultado = CriarEnemLocal(CorteEm("REDACAO"), CorteEm("FISICA"));

        FieldError erro = resultado.Errors.Single();
        erro.Field.Should().Be("regrasEliminacao[1].areaCodigo");
        erro.Error.Code.Should().Be("ConfiguracaoClassificacao.CorteEmAreaForaDoQuadro");
        erro.Error.Message.Should().Contain("FISICA").And.Contain("REDACAO (Redação)");
    }

    [Fact(DisplayName = "Corte em área que só parte dos grupos tem é recusado")]
    public void Criar_CorteEmAreaDeParteDosGrupos_Recusa()
    {
        GrupoPesoAreaEnemCongelado semMatematica = GrupoPesoAreaEnemCongelado.Criar(
            "TECNOLOGICA", "Tecnológica", "Resolução nº 805/2024/Consepe – Anexo I",
            [("REDACAO", "Redação", 2.00m, 400m), ("LINGUAGENS", "Linguagens e suas Tecnologias", 1.00m, null)]).Value!;

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoMediaPonderada() with
        {
            RegrasEliminacao = [CorteEm("MATEMATICA")],
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnem = [QuadroPesoAreaEnemDeTeste.Grupo("HUMANISTICA_I", "Humanística I"), semMatematica],
        });

        resultado.Errors.Single().Error.Code.Should().Be("ConfiguracaoClassificacao.CorteEmAreaForaDoQuadro");
    }

    [Fact(DisplayName = "Classificação importada recusa a lista inteira, sem somar a recusa do corte repetido")]
    public void Criar_ImportadaComCorteRepetido_RecusaSoAListaInteira()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoImportada() with
        {
            RegrasEliminacao = [CorteEm("REDACAO"), CorteEm("REDACAO")],
            BaseadoEmEnem = true,
        });

        resultado.Errors.Select(static e => e.Error.Code).Should().Equal("ConfiguracaoClassificacao.EliminacaoIndevida");
    }
}
