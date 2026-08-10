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
