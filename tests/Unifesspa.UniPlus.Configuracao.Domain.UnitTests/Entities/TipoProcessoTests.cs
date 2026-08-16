namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class TipoProcessoTests
{
    [Fact(DisplayName = "Cria item ativo e preserva código como identidade")]
    public void Criar_ItemValido_AtivaEPreservaCodigo()
    {
        Result<TipoProcesso> result = TipoProcesso.Criar("NOVO_TIPO", "Novo tipo", "Descrição");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Ativo.Should().BeTrue();
        result.Value.Codigo.Should().Be("NOVO_TIPO");
    }

    [Fact(DisplayName = "Recusa o sentinela universal como código de tipo de processo")]
    public void Criar_CodigoUniversal_Recusa()
    {
        Result<TipoProcesso> result = TipoProcesso.Criar("*", "Universal", null);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(TipoProcessoErrorCodes.CodigoReservado);
    }

    [Theory(DisplayName = "Recusa U+0000 em todos os campos textuais na criação")]
    [InlineData("codigo", TipoProcessoErrorCodes.CodigoComCaractereNulo)]
    [InlineData("nome", TipoProcessoErrorCodes.NomeComCaractereNulo)]
    [InlineData("descricao", TipoProcessoErrorCodes.DescricaoComCaractereNulo)]
    public void Criar_CampoComCaractereNulo_Recusa(string campo, string codigoEsperado)
    {
        string invalido = $"valor{(char)0}invalido";

        Result<TipoProcesso> result = campo switch
        {
            "codigo" => TipoProcesso.Criar(invalido, "Nome válido", "Descrição válida"),
            "nome" => TipoProcesso.Criar("CODIGO_VALIDO", invalido, "Descrição válida"),
            "descricao" => TipoProcesso.Criar("CODIGO_VALIDO", "Nome válido", invalido),
            _ => throw new InvalidOperationException($"Campo de teste inesperado: {campo}"),
        };

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(codigoEsperado);
    }

    [Theory(DisplayName = "Recusa U+0000 nos campos editáveis na atualização")]
    [InlineData("nome", TipoProcessoErrorCodes.NomeComCaractereNulo)]
    [InlineData("descricao", TipoProcessoErrorCodes.DescricaoComCaractereNulo)]
    public void Atualizar_CampoComCaractereNulo_Recusa(string campo, string codigoEsperado)
    {
        TipoProcesso tipo = TipoProcesso.Criar("CODIGO_VALIDO", "Nome válido", "Descrição válida").Value!;
        string invalido = $"valor{(char)0}invalido";

        Result result = campo switch
        {
            "nome" => tipo.Atualizar(invalido, "Descrição válida"),
            "descricao" => tipo.Atualizar("Nome válido", invalido),
            _ => throw new InvalidOperationException($"Campo de teste inesperado: {campo}"),
        };

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(codigoEsperado);
    }

    [Theory(DisplayName = "Código ou nome nulos não lançam — devolvem a violação de domínio")]
    [InlineData(null, "Nome válido", TipoProcessoErrorCodes.CodigoObrigatorio)]
    [InlineData("CODIGO_VALIDO", null, TipoProcessoErrorCodes.NomeObrigatorio)]
    public void Criar_CamposObrigatoriosNulos_NaoLancaEDevolveViolacaoDeDominio(
        string? codigo, string? nome, string codigoEsperado)
    {
        Result<TipoProcesso> resultado = TipoProcesso.Criar(codigo, nome, null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(codigoEsperado);
    }

    // ── Acumulação (ADR-0125) ──────────────────────────────────────────────────

    [Fact(DisplayName = "Código, nome e descrição inválidos ao mesmo tempo acumulam as três violações rotuladas")]
    public void Criar_TresCamposInvalidos_AcumulaAsTresViolacoesRotuladas()
    {
        Result<TipoProcesso> resultado = TipoProcesso.Criar(
            new string('A', 65), "", new string('b', 1001));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(3);
        resultado.Errors[0].Field.Should().Be("codigo");
        resultado.Errors[0].Error.Code.Should().Be(TipoProcessoErrorCodes.CodigoTamanho);
        resultado.Errors[1].Field.Should().Be("nome");
        resultado.Errors[1].Error.Code.Should().Be(TipoProcessoErrorCodes.NomeObrigatorio);
        resultado.Errors[2].Field.Should().Be("descricao");
        resultado.Errors[2].Error.Code.Should().Be(TipoProcessoErrorCodes.DescricaoTamanho);
    }

    [Fact(DisplayName = "Código reservado (\"*\") e nome ausente acumulam as duas violações")]
    public void Criar_CodigoReservadoENomeAusente_AcumulaAsDuasViolacoes()
    {
        Result<TipoProcesso> resultado = TipoProcesso.Criar("*", "", null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        resultado.Errors[0].Error.Code.Should().Be(TipoProcessoErrorCodes.CodigoReservado);
        resultado.Errors[1].Error.Code.Should().Be(TipoProcessoErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Atualizar com nome e descrição inválidos acumula as duas violações sem mutar o agregado")]
    public void Atualizar_NomeEDescricaoInvalidos_AcumulaAsDuasViolacoesSemMutar()
    {
        TipoProcesso tipo = TipoProcesso.Criar("CODIGO_VALIDO", "Nome original", null).Value!;

        Result resultado = tipo.Atualizar("", new string('a', 1001));

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().HaveCount(2);
        tipo.Nome.Should().Be("Nome original", "falha de validação não pode mutar o agregado");
    }

    [Fact(DisplayName = "Desativação é terminal e não remove a identidade")]
    public void Desativar_ItemAtivo_DesativaSemApagarCodigo()
    {
        TipoProcesso tipo = TipoProcesso.Criar("PS_TESTE", "Processo teste", null).Value!;

        Result result = tipo.Desativar();

        result.IsSuccess.Should().BeTrue();
        tipo.Ativo.Should().BeFalse();
        tipo.Codigo.Should().Be("PS_TESTE");
    }
}
