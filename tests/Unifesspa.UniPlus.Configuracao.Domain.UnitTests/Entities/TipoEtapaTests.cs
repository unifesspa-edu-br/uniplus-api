namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class TipoEtapaTests
{
    [Fact(DisplayName = "Cria item ativo e preserva código como identidade")]
    public void Criar_ItemValido_AtivaEPreservaCodigo()
    {
        Result<TipoEtapa> result = TipoEtapa.Criar("NOVO_TIPO", "Novo tipo", "Descrição", true, true);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Ativo.Should().BeTrue();
        result.Value.Codigo.Should().Be("NOVO_TIPO");
    }

    [Fact(DisplayName = "Criar normaliza codigo, nome e descricao para NFC")]
    public void Criar_ComFormaDecomposta_NormalizaParaNfc()
    {
        // Mesmo texto em duas representacoes Unicode: NFC (um unico code point por letra
        // acentuada, aqui \u00C7 e \u00C3) e NFD (letra base + combining mark) - bytes
        // diferentes, mesmo significado. Escapes em vez de literal acentuado no fonte, para o
        // teste nao depender de qual normalizacao o editor/terminal aplicaria ao salvar o
        // arquivo. Sem normalizar, duas grafias do "mesmo" codigo colidiriam com o indice unico
        // do banco só por coincidencia de bytes, e um lookup por codigo (TipoEtapaReader) feito
        // com a outra forma nunca encontraria o registro.
        const string codigoComposto = "BANCA_HETEROIDENTIFICA\u00C7\u00C3O";
        const string nomeComposto = "Banca de Heteroidentifica\u00E7\u00E3o";
        string codigoDecomposto = codigoComposto.Normalize(System.Text.NormalizationForm.FormD);
        string nomeDecomposto = nomeComposto.Normalize(System.Text.NormalizationForm.FormD);
        codigoDecomposto.Should().NotBe(codigoComposto, "pre-condicao do teste: as duas formas tem bytes diferentes");

        Result<TipoEtapa> result = TipoEtapa.Criar(codigoDecomposto, nomeDecomposto, null, true, true);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Codigo.Should().Be(codigoComposto);
        result.Value.Nome.Should().Be(nomeComposto);
    }

    [Theory(DisplayName = "Recusa U+0000 em todos os campos textuais na criação")]
    [InlineData("codigo", TipoEtapaErrorCodes.CodigoComCaractereNulo)]
    [InlineData("nome", TipoEtapaErrorCodes.NomeComCaractereNulo)]
    [InlineData("descricao", TipoEtapaErrorCodes.DescricaoComCaractereNulo)]
    public void Criar_CampoComCaractereNulo_Recusa(string campo, string codigoEsperado)
    {
        string invalido = $"valor{(char)0}invalido";

        Result<TipoEtapa> result = campo switch
        {
            "codigo" => TipoEtapa.Criar(invalido, "Nome válido", "Descrição válida", true, true),
            "nome" => TipoEtapa.Criar("CODIGO_VALIDO", invalido, "Descrição válida", true, true),
            "descricao" => TipoEtapa.Criar("CODIGO_VALIDO", "Nome válido", invalido, true, true),
            _ => throw new InvalidOperationException($"Campo de teste inesperado: {campo}"),
        };

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(codigoEsperado);
    }

    [Theory(DisplayName = "Recusa U+0000 nos campos editáveis na atualização")]
    [InlineData("nome", TipoEtapaErrorCodes.NomeComCaractereNulo)]
    [InlineData("descricao", TipoEtapaErrorCodes.DescricaoComCaractereNulo)]
    public void Atualizar_CampoComCaractereNulo_Recusa(string campo, string codigoEsperado)
    {
        TipoEtapa tipo = TipoEtapa.Criar("CODIGO_VALIDO", "Nome válido", "Descrição válida", true, true).Value!;
        string invalido = $"valor{(char)0}invalido";

        Result result = campo switch
        {
            "nome" => tipo.Atualizar(invalido, "Descrição válida", true, true),
            "descricao" => tipo.Atualizar("Nome válido", invalido, true, true),
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
        Result<TipoEtapa> resultado = TipoEtapa.Criar(codigo, nome, descricao, true, true);

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
            new string('A', 65), "", new string('b', 1001), admitePontuacao: true, admiteEliminacao: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(3);
        resultado.Errors[0].Field.Should().Be("codigo");
        resultado.Errors[0].Error.Code.Should().Be(TipoEtapaErrorCodes.CodigoTamanho);
        resultado.Errors[1].Field.Should().Be("nome");
        resultado.Errors[1].Error.Code.Should().Be(TipoEtapaErrorCodes.NomeObrigatorio);
        resultado.Errors[2].Field.Should().Be("descricao");
        resultado.Errors[2].Error.Code.Should().Be(TipoEtapaErrorCodes.DescricaoTamanho);
    }

    [Fact(DisplayName = "Atualizar com nome e descrição inválidos acumula as duas violações sem mutar o agregado")]
    public void Atualizar_NomeEDescricaoInvalidos_AcumulaAsDuasViolacoesSemMutar()
    {
        TipoEtapa tipo = TipoEtapa.Criar("CODIGO_VALIDO", "Nome original", null, true, true).Value!;

        Result resultado = tipo.Atualizar("", new string('a', 1001), true, true);

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
            "TIPO_INERTE", "Tipo inerte", null, admitePontuacao: false, admiteEliminacao: false);

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
            "TIPO_NOVO", "Tipo novo", null, admitePontuacao, admiteEliminacao);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle();
        resultado.Errors[0].Field.Should().Be(campoEsperado);
        resultado.Errors[0].Error.Code.Should().Be(codigoEsperado);
    }

    [Fact(DisplayName = "Atualizar troca o que o tipo admite")]
    public void Atualizar_TrocaOCaraterAdmitido()
    {
        TipoEtapa tipo = TipoEtapa.Criar("ANALISE", "Análise", null, true, true).Value!;

        Result resultado = tipo.Atualizar("Análise", null, admitePontuacao: false, admiteEliminacao: true);

        resultado.IsSuccess.Should().BeTrue();
        tipo.AdmitePontuacao.Should().BeFalse();
        tipo.AdmiteEliminacao.Should().BeTrue();
    }

    [Fact(DisplayName = "Atualizar sem nenhum caráter admitido recusa sem mutar o agregado")]
    public void Atualizar_SemNenhumCaraterAdmitido_RecusaSemMutar()
    {
        TipoEtapa tipo = TipoEtapa.Criar("ANALISE", "Análise", null, true, true).Value!;

        Result resultado = tipo.Atualizar("Análise", null, admitePontuacao: false, admiteEliminacao: false);

        resultado.IsFailure.Should().BeTrue();
        tipo.AdmitePontuacao.Should().BeTrue("falha de validação não pode mutar o agregado");
    }

    [Fact(DisplayName = "Desativação é terminal e não remove a identidade")]
    public void Desativar_ItemAtivo_DesativaSemApagarCodigo()
    {
        TipoEtapa tipo = TipoEtapa.Criar("PS_TESTE", "Processo teste", null, true, true).Value!;

        Result result = tipo.Desativar();

        result.IsSuccess.Should().BeTrue();
        tipo.Ativo.Should().BeFalse();
        tipo.Codigo.Should().Be("PS_TESTE");
    }
}
