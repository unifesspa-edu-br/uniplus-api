namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

public sealed class TipoConceitoEdicaoTests
{
    private static DefinicaoValidada Validada(DefinicaoInformada definicao) => definicao.Validar().Value!;

    [Fact(DisplayName = "Atualizar troca nome e descrição e preserva identidade, código e situação")]
    public void Atualizar_TrocaOsCamposEditaveis()
    {
        TipoConceito tipo = TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica()).Value!;
        Guid id = tipo.Id;

        tipo.Atualizar(Validada(DefinicoesDeTipoConceito.Numerica() with { Nome = "Novo nome", Descricao = null }));

        (tipo.Id, tipo.Codigo, tipo.Ativo).Should().Be((id, "TIPO", true));
        tipo.Nome.Should().Be("Novo nome");
        tipo.Descricao.Should().BeNull();
    }

    [Fact(DisplayName = "Atualizar substitui os valores categóricos em bloco, na nova ordem")]
    public void Atualizar_SubstituiOsValoresEmBloco()
    {
        TipoConceito tipo = TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Categorica()).Value!;
        DefinicaoInformada nova = DefinicoesDeTipoConceito.Categorica() with
        {
            Valores = [DefinicoesDeTipoConceito.Valor("C", "Gama", true), DefinicoesDeTipoConceito.Valor("A", "Alfa"), DefinicoesDeTipoConceito.Valor("B", "Beta")],
        };

        tipo.Atualizar(Validada(nova));

        tipo.Valores.Select(v => (v.Posicao, v.Codigo, v.ContaComoAprovacao)).Should().Equal((1, "C", true), (2, "A", false), (3, "B", false));
    }

    [Fact(DisplayName = "Atualizar permite trocar a natureza e descarta os parâmetros da anterior")]
    public void Atualizar_TrocaANatureza_DescartaOsParametrosAnteriores()
    {
        TipoConceito tipo = TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica()).Value!;

        tipo.Atualizar(Validada(DefinicoesDeTipoConceito.Categorica()));

        tipo.Natureza.Should().Be(NaturezaConceito.Categorico);
        tipo.EscalaMinima.Should().BeNull();
        tipo.EscalaMaxima.Should().BeNull();
        tipo.CasasDecimais.Should().BeNull();
        tipo.Valores.Should().HaveCount(2);
    }

    [Fact(DisplayName = "Atualizar de categórico para numérico descarta os valores")]
    public void Atualizar_ParaNumerico_DescartaOsValores()
    {
        TipoConceito tipo = TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Categorica()).Value!;

        tipo.Atualizar(Validada(DefinicoesDeTipoConceito.Numerica()));

        tipo.Valores.Should().BeEmpty();
        tipo.EscalaMaxima.Should().Be(10m);
    }

    [Fact(DisplayName = "Desativar tira o tipo de uso e mantém o código reservado")]
    public void Desativar_InativaOTipo()
    {
        TipoConceito tipo = TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica()).Value!;

        tipo.Desativar();

        (tipo.Ativo, tipo.Codigo).Should().Be((false, "TIPO"));
    }

    [Fact(DisplayName = "Desativar um tipo já desativado não muda nada")]
    public void Desativar_JaDesativado_ContinuaDesativado()
    {
        TipoConceito tipo = TipoConceito.Criar("TIPO", DefinicoesDeTipoConceito.Numerica()).Value!;
        tipo.Desativar();

        tipo.Desativar();

        tipo.Ativo.Should().BeFalse();
    }

    [Fact(DisplayName = "A definição validada é a que as regras aceitaram, com os valores já numerados")]
    public void Validar_DefinicaoValida_TrazOsValoresNumerados()
    {
        DefinicaoValidada validada = Validada(DefinicoesDeTipoConceito.Categorica());

        validada.Valores.Select(v => v.Posicao).Should().Equal(1, 2);
    }
}
