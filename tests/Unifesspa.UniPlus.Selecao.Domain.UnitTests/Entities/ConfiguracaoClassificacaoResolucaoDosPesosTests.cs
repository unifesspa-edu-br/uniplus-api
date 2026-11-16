namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities.ApoioDaConfiguracaoClassificacao;

public sealed class ConfiguracaoClassificacaoResolucaoDosPesosTests
{
    // ── Resolução de Pesos por Área e quadro congelado ──

    [Fact(DisplayName = "Classificação baseada em ENEM com cálculo local guarda a resolução aparada e vincula os grupos do quadro")]
    public void Criar_EnemLocalComResolucao_GuardaResolucaoEVinculaOQuadro()
    {
        IReadOnlyList<GrupoPesoAreaEnemCongelado> quadro = QuadroPesoAreaEnemDeTeste.Completo();

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoMediaPonderada() with
        {
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = $"  {QuadroPesoAreaEnemDeTeste.Resolucao} ",
            QuadroPesoAreaEnem = quadro,
        });

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.ResolucaoPesoAreaEnem.Should().Be(QuadroPesoAreaEnemDeTeste.Resolucao);
        resultado.Value.QuadroPesoAreaEnem.Should().HaveCount(4)
            .And.OnlyContain(g => g.ConfiguracaoClassificacaoId == resultado.Value.Id);
    }

    [Theory(DisplayName = "Classificação baseada em ENEM com cálculo local sem resolução é recusada no campo da resolução")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_EnemLocalSemResolucao_Recusa(string? resolucao)
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoMediaPonderada() with
        {
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = resolucao,
            QuadroPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Completo(),
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e =>
            e.Field == "resolucaoPesoAreaEnem" && e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemObrigatoria");
    }

    [Fact(DisplayName = "ADR-0125: resolução ausente é recusada no campo da resolução")]
    public void Criar_EnemLocalSemResolucao_RecusaAResolucao()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoMediaPonderada() with
        {
            BaseadoEmEnem = true,
        });

        resultado.Errors.Select(e => e.Error.Code).Should().BeEquivalentTo(
        [
            "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemObrigatoria",
        ]);
    }

    [Fact(DisplayName = "Resolução em classificação que não é baseada em ENEM é recusada como indevida")]
    public void Criar_ResolucaoSemEnem_RecusaComoIndevida()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoMediaPonderada() with
        {
            ResolucaoPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Completo(),
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e =>
            e.Field == "resolucaoPesoAreaEnem" && e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemIndevida");
    }

    [Fact(DisplayName = "Resolução em classificação importada é recusada como indevida, mesmo baseada em ENEM")]
    public void Criar_ResolucaoEmImportada_RecusaComoIndevida()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoImportada() with
        {
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Completo(),
        });

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e => e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemIndevida");
    }

    [Fact(DisplayName = "Quadro sem resolução, fora de ENEM, também é recusado como indevido")]
    public void Criar_QuadroSemResolucaoForaDeEnem_RecusaComoIndevido()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoMediaPonderada() with
        {
            QuadroPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Completo(),
        });

        resultado.Errors.Should().ContainSingle(e => e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemIndevida");
    }

    [Fact(DisplayName = "Resolução declarada sem nenhum grupo congelado é recusada")]
    public void Criar_ResolucaoComQuadroVazio_Recusa()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoMediaPonderada() with
        {
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Resolucao,
        });

        resultado.Errors.Should().ContainSingle(e => e.Error.Code == "ConfiguracaoClassificacao.QuadroPesoAreaEnemVazio");
    }

    [Fact(DisplayName = "Quadro com grupo repetido é recusado, nomeando o grupo")]
    public void Criar_QuadroComGrupoRepetido_Recusa()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoMediaPonderada() with
        {
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnem = [QuadroPesoAreaEnemDeTeste.Grupo("TECNOLOGICA", "Tecnológica"), QuadroPesoAreaEnemDeTeste.Grupo("TECNOLOGICA", "Tecnológica")],
        });

        FieldError erro = resultado.Errors.Should().ContainSingle().Subject;
        erro.Error.Code.Should().Be("ConfiguracaoClassificacao.QuadroPesoAreaEnemGrupoRepetido");
        erro.Error.Message.Should().Contain("Tecnológica");
    }

    [Fact(DisplayName = "Resolução acima da largura da coluna é recusada")]
    public void Criar_ResolucaoLongaDemais_Recusa()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoMediaPonderada() with
        {
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = new string('R', ConfiguracaoClassificacao.ResolucaoPesoAreaEnemMaxLength + 1),
            QuadroPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Completo(),
        });

        resultado.Errors.Should().ContainSingle(e => e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida");
    }

    [Theory(DisplayName = "O quadro de pesos por área é exigido só na classificação baseada em ENEM pela média ponderada")]
    [InlineData(RegraCalculoCodigo.FormulaMediaPonderada, true, true)]
    [InlineData(RegraCalculoCodigo.FormulaMediaPonderada, false, false)]
    [InlineData(RegraCalculoCodigo.ClassificacaoImportada, true, false)]
    [InlineData(RegraCalculoCodigo.ClassificacaoImportada, false, false)]
    public void ExigeQuadroPesoAreaEnem_SoNaMediaPonderadaDoEnem(string regraCalculo, bool baseadoEmEnem, bool esperado)
    {
        ReferenciaRegra regra = ReferenciaRegra.De(regraCalculo, "v1", new string('f', 64));

        ConfiguracaoClassificacao.ExigeQuadroPesoAreaEnem(regra, baseadoEmEnem).Should().Be(esperado);
    }

    [Theory(DisplayName = "A forma da resolução se confere sem a classificação: ausente passa, caractere invisível ou acima da coluna não")]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("Res. 805/2024", null)]
    [InlineData("  Resolução com exatamente quarenta caract  ", null)]
    [InlineData("805\u0000", "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida")]
    [InlineData("Res.\n805/2024", "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida")]
    [InlineData("Res. \u202E4202/508", "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida")]
    [InlineData("Res.\u2028805", "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida")]
    [InlineData("Resolução com quarenta e um caracteres ..", "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida")]
    public void ValidarResolucaoPesoAreaEnem_ConfereAForma(string? resolucao, string? codigoEsperado)
    {
        List<FieldError> erros = ConfiguracaoClassificacao.ValidarResolucaoPesoAreaEnem(resolucao);

        if (codigoEsperado is null)
        {
            erros.Should().BeEmpty();
        }
        else
        {
            erros.Should().ContainSingle(e => e.Field == "resolucaoPesoAreaEnem" && e.Error.Code == codigoEsperado);
        }
    }

    [Fact(DisplayName = "A resolução maior que o teto é recusada como inválida")]
    public void ValidarResolucaoPesoAreaEnem_ResolucaoMuitoGrande_Recusa() =>
        ConfiguracaoClassificacao.ValidarResolucaoPesoAreaEnem(new string('R', 1_000_000) + "\n1")
            .Should().ContainSingle(e => e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida");

    [Fact(DisplayName = "A resolução decomposta que, em NFC, cabe na coluna é aceita")]
    public void ValidarResolucaoPesoAreaEnem_DecompostaQueCabeEmNfc_Aceita()
    {
        // U+1F86 decompõe em quatro caracteres: 160 digitados, 40 congelados.
        string decomposta = string.Concat(Enumerable.Repeat("\u03B1\u0313\u0342\u0345", 40));

        ConfiguracaoClassificacao.ValidarResolucaoPesoAreaEnem(decomposta).Should().BeEmpty();
    }

    [Fact(DisplayName = "A resolução é gravada aparada e em NFC, a forma do cadastro e do envelope")]
    public void Criar_GravaAResolucaoEmNfc()
    {
        string decomposta = "Resolução 805/2024".Normalize(System.Text.NormalizationForm.FormD);

        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoMediaPonderada() with
        {
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = $" {decomposta} ",
            QuadroPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Completo(),
        });

        resultado.Value!.ResolucaoPesoAreaEnem.Should().Be(
            "Resolução 805/2024".Normalize(System.Text.NormalizationForm.FormC),
            "o cadastro grava em NFC e o envelope emite em NFC: sem a mesma forma aqui, o valor muda num ciclo de retificação");
    }

    [Fact(DisplayName = "Resolução com surrogate sem par é recusada como inválida, sem exceção da normalização")]
    public void Criar_ResolucaoComSurrogateSemPar_Recusa()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoMediaPonderada() with
        {
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = "Res." + (char)0xD800,
            QuadroPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Completo(),
        });

        resultado.Errors.Should().ContainSingle(e => e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida");
    }

    [Fact(DisplayName = "Sem as consequências da resolução recusada saem só a forma repetida e o quadro vazio, e o resto fica, inclusive no mesmo campo")]
    public void SemConsequenciasDaResolucaoRecusada_TiraSoAsConsequencias()
    {
        FieldError invalida = new(ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem, new DomainError("ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida", "x"));
        FieldError quadroVazio = new(ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem, new DomainError("ConfiguracaoClassificacao.QuadroPesoAreaEnemVazio", "x"));
        FieldError outraNoMesmoCampo = new(ConfiguracaoClassificacao.CampoResolucaoPesoAreaEnem, new DomainError("ConfiguracaoClassificacao.QuadroPesoAreaEnemGrupoRepetido", "x"));
        FieldError casas = new("casasArredondamento", new DomainError("ConfiguracaoClassificacao.CasasArredondamentoObrigatorio", "x"));

        ConfiguracaoClassificacao.SemConsequenciasDaResolucaoRecusada([invalida, quadroVazio, outraNoMesmoCampo, casas])
            .Should().Equal(outraNoMesmoCampo, casas);
    }

    [Fact(DisplayName = "Não-caractere na resolução é recusado como inválido, sem exceção da normalização")]
    public void ValidarResolucaoPesoAreaEnem_NaoCaractere_Recusa() =>
        ConfiguracaoClassificacao.ValidarResolucaoPesoAreaEnem("Res. 805" + (char)0xFFFE)
            .Should().ContainSingle(e => e.Error.Code == "ConfiguracaoClassificacao.ResolucaoPesoAreaEnemInvalida");

    [Fact(DisplayName = "O grupo repetido é ecoado sem caractere invisível")]
    public void Criar_QuadroComGrupoRepetido_EcoaORotuloSaneado()
    {
        Result<ConfiguracaoClassificacao> resultado = ConfiguracaoClassificacao.Criar(DeclaracaoMediaPonderada() with
        {
            BaseadoEmEnem = true,
            ResolucaoPesoAreaEnem = QuadroPesoAreaEnemDeTeste.Resolucao,
            QuadroPesoAreaEnem = [QuadroPesoAreaEnemDeTeste.Grupo("TECNOLOGICA", "Tecno\u202Elógica"), QuadroPesoAreaEnemDeTeste.Grupo("TECNOLOGICA", "Tecno\u202Elógica")],
        });

        string mensagem = resultado.Errors.Single(e => e.Error.Code == "ConfiguracaoClassificacao.QuadroPesoAreaEnemGrupoRepetido").Error.Message;
        mensagem.Should().Contain("Tecno?lógica").And.NotContain("\u202E");
    }
}
