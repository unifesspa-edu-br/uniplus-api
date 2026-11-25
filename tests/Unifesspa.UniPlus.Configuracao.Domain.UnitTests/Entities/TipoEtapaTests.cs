namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class TipoEtapaTests
{
    [Fact(DisplayName = "Cria item ativo e preserva código como identidade")]
    public void Criar_ItemValido_AtivaEPreservaCodigo()
    {
        Result<TipoEtapa> result = TipoEtapa.Criar("NOVO_TIPO", "Novo tipo", "Descrição", true, true, false);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Ativo.Should().BeTrue();
        result.Value.Codigo.Should().Be("NOVO_TIPO");
    }

    [Theory(DisplayName = "Recusa U+0000 em todos os campos textuais na criação")]
    [InlineData("codigo", TipoEtapaErrorCodes.CodigoFormatoInvalido)]
    [InlineData("nome", TipoEtapaErrorCodes.NomeCaractereInvalido)]
    [InlineData("descricao", TipoEtapaErrorCodes.DescricaoCaractereInvalido)]
    public void Criar_CampoComCaractereNulo_Recusa(string campo, string codigoEsperado)
    {
        string invalido = $"valor{(char)0}invalido";

        Result<TipoEtapa> result = campo switch
        {
            "codigo" => TipoEtapa.Criar(invalido, "Nome válido", "Descrição válida", true, true, false),
            "nome" => TipoEtapa.Criar("CODIGO_VALIDO", invalido, "Descrição válida", true, true, false),
            "descricao" => TipoEtapa.Criar("CODIGO_VALIDO", "Nome válido", invalido, true, true, false),
            _ => throw new InvalidOperationException($"Campo de teste inesperado: {campo}"),
        };

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(codigoEsperado);
    }

    [Theory(DisplayName = "Código e nome de uma linha recusam tabulação, quebra de linha e formatação")]
    [InlineData("CODIGO\tA")]
    [InlineData("CODIGO\nA")]
    [InlineData("CODIGO\u202EA")]
    public void Criar_CodigoENomeComInvisivel_Recusam(string texto)
    {
        TipoEtapa.Criar(texto, "Nome válido", null, true, true, false).Error!.Code.Should().Be(TipoEtapaErrorCodes.CodigoFormatoInvalido);
        TipoEtapa.Criar("CODIGO_VALIDO", texto, null, true, true, false).Error!.Code.Should().Be(TipoEtapaErrorCodes.NomeCaractereInvalido);
    }

    [Fact(DisplayName = "A descrição aceita várias linhas e tabulação e recusa os demais controles")]
    public void Criar_DescricaoDeVariasLinhas_AceitaEControleRecusa()
    {
        TipoEtapa.Criar("CODIGO_VALIDO", "Nome válido", "linha 1\r\nlinha\t2", true, true, false).IsSuccess.Should().BeTrue();
        TipoEtapa.Criar("CODIGO_VALIDO", "Nome válido", "linha\u0007", true, true, false)
            .Error!.Code.Should().Be(TipoEtapaErrorCodes.DescricaoCaractereInvalido);
    }

    [Theory(DisplayName = "Recusa U+0000 nos campos editáveis na atualização")]
    [InlineData("nome", TipoEtapaErrorCodes.NomeCaractereInvalido)]
    [InlineData("descricao", TipoEtapaErrorCodes.DescricaoCaractereInvalido)]
    public void Atualizar_CampoComCaractereNulo_Recusa(string campo, string codigoEsperado)
    {
        TipoEtapa tipo = TipoEtapa.Criar("CODIGO_VALIDO", "Nome válido", "Descrição válida", true, true, false).Value!;
        string invalido = $"valor{(char)0}invalido";

        Result result = campo switch
        {
            "nome" => tipo.Atualizar(invalido, "Descrição válida", true, true, false),
            "descricao" => tipo.Atualizar("Nome válido", invalido, true, true, false),
            _ => throw new InvalidOperationException($"Campo de teste inesperado: {campo}"),
        };

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(codigoEsperado);
    }

    [Theory(DisplayName = "Código, nome ou descrição nulos não lançam — devolvem a violação de domínio")]
    [InlineData(null, "Nome válido", null, TipoEtapaErrorCodes.CodigoObrigatorio)]
    [InlineData("CODIGO_VALIDO", null, null, TipoEtapaErrorCodes.NomeObrigatorio)]
    public void Criar_CamposObrigatoriosNulos_NaoLancaEDevolveViolacaoDeDominio(
        string? codigo, string? nome, string? descricao, string codigoEsperado)
    {
        Result<TipoEtapa> resultado = TipoEtapa.Criar(codigo, nome, descricao, true, true, false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(codigoEsperado);
    }

    // ── Acumulação (ADR-0125) ──────────────────────────────────────────────────

    /// <summary>
    /// Antes: ValidarCampos retornava no primeiro campo inválido, mascarando as
    /// demais violações — paridade com o FluentValidation removido, que
    /// reportava cada campo em separado.
    /// </summary>
    [Fact(DisplayName = "Código, nome e descrição inválidos ao mesmo tempo acumulam as três violações rotuladas")]
    public void Criar_TresCamposInvalidos_AcumulaAsTresViolacoesRotuladas()
    {
        Result<TipoEtapa> resultado = TipoEtapa.Criar(
            new string('A', 81), "", new string('b', 1001), admitePontuacao: true, admiteEliminacao: true, exigeCorrecaoIndependente: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(3);
        resultado.Errors[0].Field.Should().Be("codigo");
        resultado.Errors[0].Error.Code.Should().Be(TipoEtapaErrorCodes.CodigoFormatoInvalido);
        resultado.Errors[1].Field.Should().Be("nome");
        resultado.Errors[1].Error.Code.Should().Be(TipoEtapaErrorCodes.NomeObrigatorio);
        resultado.Errors[2].Field.Should().Be("descricao");
        resultado.Errors[2].Error.Code.Should().Be(TipoEtapaErrorCodes.DescricaoTamanho);
    }

    [Fact(DisplayName = "Atualizar com nome e descrição inválidos acumula as duas violações sem mutar o agregado")]
    public void Atualizar_NomeEDescricaoInvalidos_AcumulaAsDuasViolacoesSemMutar()
    {
        TipoEtapa tipo = TipoEtapa.Criar("CODIGO_VALIDO", "Nome original", null, true, true, false).Value!;

        Result resultado = tipo.Atualizar("", new string('a', 1001), true, true, false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        tipo.Nome.Should().Be("Nome original", "falha de validação não pode mutar o agregado");
    }

    /// <summary>
    /// Toda etapa do certame declara um caráter. Um tipo que não compõe a nota final nem elimina
    /// candidato não deixa caráter nenhum para escolher — não configura etapa.
    /// </summary>
    [Fact(DisplayName = "Tipo que não compõe nota nem elimina é recusado")]
    public void Criar_SemNenhumCaraterAdmitido_Recusa()
    {
        Result<TipoEtapa> resultado = TipoEtapa.Criar(
            "TIPO_INERTE", "Tipo inerte", null, admitePontuacao: false, admiteEliminacao: false, exigeCorrecaoIndependente: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Error.Code.Should().Be(TipoEtapaErrorCodes.SemCaraterAdmitido);
    }

    /// <summary>
    /// Sinalizador ausente no payload não pode virar <c>false</c> em silêncio: o tipo nasceria
    /// declarando que não compõe a nota final sem ninguém ter dito isso.
    /// </summary>
    [Theory(DisplayName = "Sinalizador de caráter admitido ausente é recusado por campo")]
    [InlineData(null, true, "admitePontuacao", TipoEtapaErrorCodes.AdmitePontuacaoObrigatorio)]
    [InlineData(true, null, "admiteEliminacao", TipoEtapaErrorCodes.AdmiteEliminacaoObrigatorio)]
    public void Criar_SinalizadorAusente_RecusaComCampo(
        bool? admitePontuacao, bool? admiteEliminacao, string campoEsperado, string codigoEsperado)
    {
        Result<TipoEtapa> resultado = TipoEtapa.Criar(
            "TIPO_NOVO", "Tipo novo", null, admitePontuacao, admiteEliminacao, exigeCorrecaoIndependente: false);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be(campoEsperado);
        resultado.Errors[0].Error.Code.Should().Be(codigoEsperado);
    }

    [Fact(DisplayName = "Atualizar troca o que o tipo admite")]
    public void Atualizar_TrocaOCaraterAdmitido()
    {
        TipoEtapa tipo = TipoEtapa.Criar("ANALISE", "Análise", null, true, true, false).Value!;

        Result resultado = tipo.Atualizar("Análise", null, admitePontuacao: false, admiteEliminacao: true, exigeCorrecaoIndependente: false);

        resultado.IsSuccess.Should().BeTrue();
        tipo.AdmitePontuacao.Should().BeFalse();
        tipo.AdmiteEliminacao.Should().BeTrue();
    }

    [Fact(DisplayName = "Atualizar sem nenhum caráter admitido recusa sem mutar o agregado")]
    public void Atualizar_SemNenhumCaraterAdmitido_RecusaSemMutar()
    {
        TipoEtapa tipo = TipoEtapa.Criar("ANALISE", "Análise", null, true, true, false).Value!;

        Result resultado = tipo.Atualizar("Análise", null, admitePontuacao: false, admiteEliminacao: false, exigeCorrecaoIndependente: false);

        resultado.IsFailure.Should().BeTrue();
        tipo.AdmitePontuacao.Should().BeTrue("falha de validação não pode mutar o agregado");
    }

    [Fact(DisplayName = "Desativação é terminal e não remove a identidade")]
    public void Desativar_ItemAtivo_DesativaSemApagarCodigo()
    {
        TipoEtapa tipo = TipoEtapa.Criar("PS_TESTE", "Processo teste", null, true, true, false).Value!;

        Result result = tipo.Desativar();

        result.IsSuccess.Should().BeTrue();
        tipo.Ativo.Should().BeFalse();
        tipo.Codigo.Should().Be("PS_TESTE");
    }

    [Fact(DisplayName = "Tipo criado pela API não declara nota de origem no ENEM")]
    public void Criar_NaoDeclaraNotaDeOrigemNoEnem()
    {
        TipoEtapa.Criar("NOVO_TIPO", "Novo tipo", null, true, true, false).Value!.NotaDeOrigemNoEnem.Should().BeFalse();
    }

    [Fact(DisplayName = "Tipo com nota de origem no ENEM recusa desligar a pontuação, sem mudar nada")]
    public void Atualizar_NotaDeOrigemNoEnemSemPontuacao_Recusa()
    {
        TipoEtapa tipo = TipoEtapaDeTeste.NotaDoEnem();

        Result result = tipo.Atualizar("Nota do ENEM (renomeada)", null, admitePontuacao: false, admiteEliminacao: true, exigeCorrecaoIndependente: false);

        result.IsFailure.Should().BeTrue();
        result.Errors.Should().ContainSingle().Which.Should().Be(new FieldError("admitePontuacao", new DomainError(
            TipoEtapaErrorCodes.NotaDeOrigemNoEnemExigePontuacao,
            "O tipo de etapa com nota de origem no ENEM deve admitir compor a nota final.")));
        tipo.AdmitePontuacao.Should().BeTrue();
        tipo.Nome.Should().Be("Nota do ENEM");
        tipo.NotaDeOrigemNoEnem.Should().BeTrue();
    }

    [Fact(DisplayName = "A recusa da pontuação no tipo do ENEM acumula com as violações dos campos")]
    public void Atualizar_NotaDeOrigemNoEnemSemPontuacaoENomeVazio_AcumulaAsDuas()
    {
        Result result = TipoEtapaDeTeste.NotaDoEnem().Atualizar("", null, admitePontuacao: false, admiteEliminacao: true, exigeCorrecaoIndependente: false);

        result.Errors.Select(erro => erro.Error.Code).Should().Equal(
            TipoEtapaErrorCodes.NomeObrigatorio, TipoEtapaErrorCodes.NotaDeOrigemNoEnemExigePontuacao);
    }

    [Fact(DisplayName = "Tipo com nota de origem no ENEM aceita mudar nome e eliminação mantendo a pontuação")]
    public void Atualizar_NotaDeOrigemNoEnemComPontuacao_Aceita()
    {
        TipoEtapa tipo = TipoEtapaDeTeste.NotaDoEnem();

        tipo.Atualizar("Nota do ENEM (renomeada)", null, admitePontuacao: true, admiteEliminacao: false, exigeCorrecaoIndependente: false).IsSuccess.Should().BeTrue();

        tipo.AdmiteEliminacao.Should().BeFalse();
        tipo.NotaDeOrigemNoEnem.Should().BeTrue("a origem da nota é identidade, e a atualização não a toca");
    }

    [Fact(DisplayName = "Tipo criado com a correção independente exigida guarda o sinalizador")]
    public void Criar_ExigeCorrecaoIndependente_GuardaOSinalizador()
    {
        TipoEtapa.Criar("REDACAO_TESTE", "Redação", null, true, true, true).Value!.ExigeCorrecaoIndependente.Should().BeTrue();
    }

    [Fact(DisplayName = "Sinalizador de correção independente ausente é recusado na criação e na atualização, no campo próprio")]
    public void ExigeCorrecaoIndependenteAusente_Recusa()
    {
        FieldError esperado = new("exigeCorrecaoIndependente", new DomainError(
            TipoEtapaErrorCodes.ExigeCorrecaoIndependenteObrigatorio,
            "É obrigatório declarar se o tipo de etapa exige correção independente."));
        TipoEtapa tipo = TipoEtapa.Criar("ANALISE", "Análise", null, true, true, false).Value!;

        TipoEtapa.Criar("NOVO_TIPO", "Novo tipo", null, true, true, null).Errors.Should().ContainSingle().Which.Should().Be(esperado);
        tipo.Atualizar("Análise", null, true, true, null).Errors.Should().ContainSingle().Which.Should().Be(esperado);
    }

    [Fact(DisplayName = "Atualizar liga e desliga a correção independente")]
    public void Atualizar_AlternaACorrecaoIndependente()
    {
        TipoEtapa tipo = TipoEtapa.Criar("ANALISE", "Análise", null, true, true, false).Value!;

        tipo.Atualizar("Análise", null, true, true, true).IsSuccess.Should().BeTrue();
        tipo.ExigeCorrecaoIndependente.Should().BeTrue();
        tipo.Atualizar("Análise", null, true, true, false).IsSuccess.Should().BeTrue();
        tipo.ExigeCorrecaoIndependente.Should().BeFalse();
    }

    [Fact(DisplayName = "Tipo com nota de origem no ENEM recusa exigir correção independente, sem mudar nada")]
    public void Atualizar_NotaDeOrigemNoEnemComCorrecaoIndependente_Recusa()
    {
        TipoEtapa tipo = TipoEtapaDeTeste.NotaDoEnem();

        Result result = tipo.Atualizar("Nota do ENEM", null, true, true, true);

        result.Errors.Should().ContainSingle().Which.Should().Be(new FieldError("exigeCorrecaoIndependente", new DomainError(
            TipoEtapaErrorCodes.CorrecaoIndependenteComNotaDeOrigemNoEnem,
            "O tipo de etapa com nota de origem no ENEM não admite correção independente.")));
        tipo.ExigeCorrecaoIndependente.Should().BeFalse();
    }

    [Fact(DisplayName = "Exigir correção independente sem conceito admitido é uma violação do campo dos conceitos")]
    public void ViolacoesDaCorrecaoSemConceito_SemConceito_ApontaOCampoDosConceitos()
    {
        TipoEtapa.ViolacoesDaCorrecaoSemConceito(true, []).Should().ContainSingle().Which.Should().Be(new FieldError("conceitosAdmitidos", new DomainError(
            TipoEtapaErrorCodes.CorrecaoIndependenteSemConceito,
            "O tipo de etapa que exige correção independente deve admitir ao menos um tipo de conceito.")));
    }

    [Theory(DisplayName = "Sem exigir correção independente, ou com conceito admitido, não há violação de conceito")]
    [InlineData(false, 0)]
    [InlineData(null, 0)]
    [InlineData(true, 1)]
    public void ViolacoesDaCorrecaoSemConceito_SemViolacao(bool? exige, int conceitos)
    {
        TipoConceito[] admitidos = [.. Enumerable.Range(0, conceitos).Select(_ => TipoConceito.Criar("NOTA_ENTREVISTA", DefinicoesDeTipoConceito.Numerica()).Value!)];

        TipoEtapa.ViolacoesDaCorrecaoSemConceito(exige, admitidos).Should().BeEmpty();
    }

    [Fact(DisplayName = "Tipo com nota de origem no ENEM não pode ser removido")]
    public void Desativar_NotaDeOrigemNoEnem_Recusa()
    {
        TipoEtapa tipo = TipoEtapaDeTeste.NotaDoEnem();

        Result result = tipo.Desativar();

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TipoEtapaErrorCodes.NotaDeOrigemNoEnemNaoDesativa);
        tipo.Ativo.Should().BeTrue();
    }
}
