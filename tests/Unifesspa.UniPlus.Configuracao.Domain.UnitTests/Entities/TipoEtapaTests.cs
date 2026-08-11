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
        Result<TipoEtapa> result = TipoEtapa.Criar("NOVO_TIPO", "Novo tipo", "Descrição");

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

        Result<TipoEtapa> result = TipoEtapa.Criar(codigoDecomposto, nomeDecomposto, null);

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
            "codigo" => TipoEtapa.Criar(invalido, "Nome válido", "Descrição válida"),
            "nome" => TipoEtapa.Criar("CODIGO_VALIDO", invalido, "Descrição válida"),
            "descricao" => TipoEtapa.Criar("CODIGO_VALIDO", "Nome válido", invalido),
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
        TipoEtapa tipo = TipoEtapa.Criar("CODIGO_VALIDO", "Nome válido", "Descrição válida").Value!;
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
        TipoEtapa tipo = TipoEtapa.Criar("PS_TESTE", "Processo teste", null).Value!;

        Result result = tipo.Desativar();

        result.IsSuccess.Should().BeTrue();
        tipo.Ativo.Should().BeFalse();
        tipo.Codigo.Should().Be("PS_TESTE");
    }
}
