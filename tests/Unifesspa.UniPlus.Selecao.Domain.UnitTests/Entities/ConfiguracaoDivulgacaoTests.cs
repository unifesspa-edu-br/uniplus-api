namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

public sealed class ConfiguracaoDivulgacaoTests
{
    [Theory(DisplayName = "Criar aceita os três tokens do vocabulário fechado")]
    [InlineData(new[] { "NUMERO_INSCRICAO" }, null)]
    [InlineData(new[] { "NUMERO_INSCRICAO", "NOME_ABREVIADO" }, null)]
    [InlineData(new[] { "NUMERO_INSCRICAO", "NOME" }, "Ampliação para dar transparência ao resultado.")]
    public void Criar_TresTokensDoVocabulario_Sucesso(string[] camposPublicos, string? justificativa)
    {
        Result<ConfiguracaoDivulgacao> resultado = ConfiguracaoDivulgacao.Criar(camposPublicos, justificativa);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Criar com lista vazia falha — CamposPublicosVazio")]
    public void Criar_ListaVazia_Falha()
    {
        Result<ConfiguracaoDivulgacao> resultado = ConfiguracaoDivulgacao.Criar([], null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDivulgacao.CamposPublicosVazio");
    }

    [Fact(DisplayName = "Criar com campo fora do vocabulário falha — CampoNaoPermitido")]
    public void Criar_CampoNaoPermitido_Falha()
    {
        Result<ConfiguracaoDivulgacao> resultado = ConfiguracaoDivulgacao.Criar(["NUMERO_INSCRICAO", "cpf"], null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDivulgacao.CampoNaoPermitido");
    }

    [Fact(DisplayName = "Criar sem NUMERO_INSCRICAO falha — NumeroInscricaoObrigatorio")]
    public void Criar_SemNumeroInscricao_Falha()
    {
        Result<ConfiguracaoDivulgacao> resultado = ConfiguracaoDivulgacao.Criar(["NOME_ABREVIADO"], null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDivulgacao.NumeroInscricaoObrigatorio");
    }

    [Fact(DisplayName = "Criar com NOME_ABREVIADO e NOME juntos falha — FormasDeIdentificacaoExcludentes")]
    public void Criar_NomeENomeAbreviadoJuntos_Falha()
    {
        Result<ConfiguracaoDivulgacao> resultado = ConfiguracaoDivulgacao.Criar(
            ["NUMERO_INSCRICAO", "NOME_ABREVIADO", "NOME"], "Justificativa qualquer.");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDivulgacao.FormasDeIdentificacaoExcludentes");
    }

    [Fact(DisplayName = "Criar com nome sem justificativa falha — JustificativaObrigatoria")]
    public void Criar_NomeSemJustificativa_Falha()
    {
        Result<ConfiguracaoDivulgacao> resultado = ConfiguracaoDivulgacao.Criar(["NUMERO_INSCRICAO", "NOME"], null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDivulgacao.JustificativaObrigatoria");
    }

    [Fact(DisplayName = "Criar com justificativa só de espaços é tratado como ausente — JustificativaObrigatoria")]
    public void Criar_NomeComJustificativaEmBranco_Falha()
    {
        Result<ConfiguracaoDivulgacao> resultado = ConfiguracaoDivulgacao.Criar(["NUMERO_INSCRICAO", "NOME"], "   ");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDivulgacao.JustificativaObrigatoria");
    }

    [Fact(DisplayName = "Criar com justificativa acima do limite falha — JustificativaMuitoLonga")]
    public void Criar_JustificativaMuitoLonga_Falha()
    {
        string justificativaMuitoLonga = new('a', ConfiguracaoDivulgacao.JustificativaMaxLength + 1);

        Result<ConfiguracaoDivulgacao> resultado = ConfiguracaoDivulgacao.Criar(["NUMERO_INSCRICAO", "NOME"], justificativaMuitoLonga);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDivulgacao.JustificativaMuitoLonga");
    }

    [Fact(DisplayName = "Criar com justificativa contendo o caractere nulo falha — JustificativaCaractereInvalido")]
    public void Criar_JustificativaComCaractereNulo_Falha()
    {
        // U+0000 sobrevive a IsNullOrWhiteSpace, Trim e NormalizeNfc — nenhum dos três o
        // reconhece nem o remove. Sem este guard o valor chegaria ao SaveChanges e o
        // PostgreSQL recusaria o byte zero em coluna de texto (500 em vez de erro de domínio).
        string justificativaComCaractereNulo = $"Justificativa com {(char)0} no meio.";

        Result<ConfiguracaoDivulgacao> resultado = ConfiguracaoDivulgacao.Criar(
            ["NUMERO_INSCRICAO", "NOME"], justificativaComCaractereNulo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDivulgacao.JustificativaCaractereInvalido");
    }

    [Fact(DisplayName = "Criar com justificativa de várias linhas e tabulação é aceita")]
    public void Criar_JustificativaComQuebraDeLinhaETabulacao_Sucesso()
    {
        Result<ConfiguracaoDivulgacao> resultado = ConfiguracaoDivulgacao.Criar(
            ["NUMERO_INSCRICAO", "NOME"], "Primeira linha.\r\n\tSegunda linha.\nTerceira.");

        resultado.IsSuccess.Should().BeTrue();
    }

    [Theory(DisplayName = "Criar com justificativa contendo controle ou formatação falha — JustificativaCaractereInvalido")]
    [InlineData("Justificativa com \u0007 sinal.")]
    [InlineData("Justificativa com \u202E inversão.")]
    public void Criar_JustificativaComControleOuFormatacao_Falha(string justificativa)
    {
        Result<ConfiguracaoDivulgacao> resultado = ConfiguracaoDivulgacao.Criar(["NUMERO_INSCRICAO", "NOME"], justificativa);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ConfiguracaoDivulgacao.JustificativaCaractereInvalido");
    }

    [Fact(DisplayName = "Violar vocabulário e piso ao mesmo tempo acumula os dois erros no mesmo lote")]
    public void Criar_VocabularioEPisoViolados_AcumulaOsDois()
    {
        // "cpf" não pertence ao vocabulário E a lista não contém NUMERO_INSCRICAO — as duas
        // invariantes estão violadas ao mesmo tempo (ADR-0125: ambas entram no mesmo lote).
        Result<ConfiguracaoDivulgacao> resultado = ConfiguracaoDivulgacao.Criar(["cpf"], null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors.Should().Contain(e => e.Field == "camposPublicos" && e.Error.Code == "ConfiguracaoDivulgacao.CampoNaoPermitido");
        resultado.Errors.Should().Contain(e => e.Field == "camposPublicos" && e.Error.Code == "ConfiguracaoDivulgacao.NumeroInscricaoObrigatorio");
    }

    [Fact(DisplayName = "Item nulo na lista não pertence ao vocabulário — não sobrevive silenciosamente à lista canônica")]
    public void Criar_ComItemNuloNaLista_RetornaCampoNaoPermitido()
    {
        Result<ConfiguracaoDivulgacao> resultado = ConfiguracaoDivulgacao.Criar(["NUMERO_INSCRICAO", null!], null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle(e => e.Field == "camposPublicos" && e.Error.Code == "ConfiguracaoDivulgacao.CampoNaoPermitido");
    }

    [Fact(DisplayName = "Justificativa longa demais e com caractere nulo ao mesmo tempo acumula as duas violações")]
    public void Criar_JustificativaLongaComCaractereNulo_AcumulaAsDuasViolacoes()
    {
        string justificativa = $"{(char)0}{new string('a', ConfiguracaoDivulgacao.JustificativaMaxLength + 1)}";

        Result<ConfiguracaoDivulgacao> resultado = ConfiguracaoDivulgacao.Criar(["NUMERO_INSCRICAO", "NOME"], justificativa);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors.Should().Contain(e => e.Field == "justificativa" && e.Error.Code == "ConfiguracaoDivulgacao.JustificativaCaractereInvalido");
        resultado.Errors.Should().Contain(e => e.Field == "justificativa" && e.Error.Code == "ConfiguracaoDivulgacao.JustificativaMuitoLonga");
    }

    [Fact(DisplayName = "Nome sem justificativa e NOME_ABREVIADO junto de nome acumulam os dois erros")]
    public void Criar_ComDoisErrosIndependentes_AcumulaOsDois()
    {
        Result<ConfiguracaoDivulgacao> resultado = ConfiguracaoDivulgacao.Criar(
            ["NUMERO_INSCRICAO", "NOME_ABREVIADO", "NOME"], null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors.Should().Contain(e => e.Field == "camposPublicos" && e.Error.Code == "ConfiguracaoDivulgacao.FormasDeIdentificacaoExcludentes");
        resultado.Errors.Should().Contain(e => e.Field == "justificativa" && e.Error.Code == "ConfiguracaoDivulgacao.JustificativaObrigatoria");
    }

    [Fact(DisplayName = "O conjunto é deduplicado e guardado na ordem canônica, não na ordem de entrada")]
    public void Criar_DeduplicaEOrdenaCanonicamente()
    {
        Result<ConfiguracaoDivulgacao> ordemA = ConfiguracaoDivulgacao.Criar(["NOME_ABREVIADO", "NUMERO_INSCRICAO"], null);
        Result<ConfiguracaoDivulgacao> ordemB = ConfiguracaoDivulgacao.Criar(["NUMERO_INSCRICAO", "NOME_ABREVIADO", "NUMERO_INSCRICAO"], null);

        ordemA.IsSuccess.Should().BeTrue();
        ordemB.IsSuccess.Should().BeTrue();

        // Duas entradas semanticamente iguais (mesmo conjunto, ordem/repetição diferentes na
        // requisição) produzem a MESMA coleção persistida — é o que evita um UPDATE espúrio do
        // ValueComparer sequencial do jsonb a cada requisição equivalente.
        ordemA.Value!.CamposPublicos.Should().Equal(ordemB.Value!.CamposPublicos);
        // Ordem ordinal (ASCII): "NOME_ABREVIADO" < "NUMERO_INSCRICAO" ('o' < 'u' na segunda posição).
        ordemA.Value!.CamposPublicos.Should().Equal("NOME_ABREVIADO", "NUMERO_INSCRICAO");
    }

    [Fact(DisplayName = "Justificativa é normalizada (Trim + NFC) na criação")]
    public void Criar_NormalizaJustificativa()
    {
        // "e" + combinante agudo (U+0301, NFD) — a forma pré-composta ("é", NFC) é o que o
        // canonicalizador do envelope emitiria; a entidade tem de guardar a mesma forma, senão o
        // valor persistido divergiria do que a publicação congela.
        string decombinada = "  Justificativa com é acentuado nas bordas  ";

        Result<ConfiguracaoDivulgacao> resultado = ConfiguracaoDivulgacao.Criar(["NUMERO_INSCRICAO", "NOME"], decombinada);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Justificativa.Should().Be("Justificativa com é acentuado nas bordas");
    }

    [Theory(DisplayName = "EhDefaultMinimizado só reconhece a forma exata do default")]
    [InlineData(new[] { "NUMERO_INSCRICAO" }, null, true)]
    [InlineData(new[] { "NUMERO_INSCRICAO" }, "algo", false)]
    [InlineData(new[] { "NUMERO_INSCRICAO", "NOME_ABREVIADO" }, null, false)]
    public void EhDefaultMinimizado_ReconheceApenasAFormaExata(string[] camposPublicos, string? justificativa, bool esperado)
    {
        ConfiguracaoDivulgacao.EhDefaultMinimizado(camposPublicos, justificativa).Should().Be(esperado);
    }
}
