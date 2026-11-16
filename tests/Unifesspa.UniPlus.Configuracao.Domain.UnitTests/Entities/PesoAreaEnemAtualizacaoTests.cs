namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.PesoAreaEnemDeTeste;

public sealed class PesoAreaEnemAtualizacaoTests
{
    [Fact(DisplayName = "Atualizar muda peso, corte e base legal, sem mexer em código, rótulo, resolução e grupo")]
    public void Atualizar_MudaSoOsValores()
    {
        PesoAreaEnem peso = Criar().Value!;
        Guid id = peso.Id;
        List<AreaInformada> novas = AreasValidas();
        novas[0] = new(PesoAreaEnem.CodigoRedacao, 3m, null);
        novas[4] = new(PesoAreaEnem.CodigoMatematica, 4.25m, null);

        Result resultado = peso.Atualizar(novas, "  Nova base  ");

        resultado.IsSuccess.Should().BeTrue();
        peso.Id.Should().Be(id);
        peso.Resolucao.Should().Be(Resolucao);
        peso.GrupoCurso.Codigo.Should().Be(Grupo);
        peso.BaseLegal.Should().Be("Nova base");
        peso.AreasDaLinha[0].Peso.Should().Be(3m);
        peso.AreasDaLinha[0].Corte.Should().BeNull();
        peso.AreasDaLinha[4].Peso.Should().Be(4.25m);
        peso.AreasDaLinha.Select(a => (a.Codigo, a.Rotulo)).Should().Equal(
            PesoAreaEnem.Areas.Select(a => (a.Codigo, a.Rotulo)));
    }

    [Fact(DisplayName = "Atualizar mantém a mesma instância de cada área: muda os valores no lugar")]
    public void Atualizar_MudaNoLugar()
    {
        PesoAreaEnem peso = Criar().Value!;
        PesoAreaEnemArea[] antes = [.. peso.AreasDaLinha];

        peso.Atualizar(AreasValidas(), BaseLegal).IsSuccess.Should().BeTrue();

        peso.AreasDaLinha.Should().HaveCount(5);
        for (int i = 0; i < antes.Length; i++)
        {
            peso.AreasDaLinha[i].Should().BeSameAs(antes[i]);
        }
    }

    [Fact(DisplayName = "Atualizar com área inválida falha e não muda nada")]
    public void Atualizar_Invalido_NaoMuda()
    {
        PesoAreaEnem peso = Criar().Value!;

        Result resultado = peso.Atualizar(ComArea(1, new(PesoAreaEnem.CodigoCienciasDaNatureza, -1m, null)), "Outra base");

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Single().Field.Should().Be("areas[1].peso");
        peso.AreasDaLinha[1].Peso.Should().Be(1.50m);
        peso.BaseLegal.Should().Be(BaseLegal);
    }

    [Fact(DisplayName = "ValidarCamposDoPayload recusa sem instância, com o mesmo campo da gravação")]
    public void ValidarCamposDoPayload_Recusa()
    {
        Result resultado = PesoAreaEnem.ValidarCamposDoPayload(
            ComArea(2, new(PesoAreaEnem.CodigoCienciasHumanas, 1m, 1500m)), BaseLegal);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Single().Field.Should().Be("areas[2].corte");
    }

    [Fact(DisplayName = "ValidarCamposDoPayload aceita o payload válido")]
    public void ValidarCamposDoPayload_Aceita()
    {
        PesoAreaEnem.ValidarCamposDoPayload(AreasValidas(), BaseLegal).IsSuccess.Should().BeTrue();
    }
}
