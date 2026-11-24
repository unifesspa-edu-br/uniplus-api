namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

public sealed class CidadeDeProvaNoModeloSemeadoTests
{
    [Fact]
    public void Inscricao_ComProva_PedeCidadeEmSecaoSempreVisivel()
    {
        ConteudoDoModelo modelo = ModelosDaSementePsrMedicina2027.Inscricao(comProva: true);
        EtapaDoModelo secao = modelo.Etapas.Should().ContainSingle(static e => e.Codigo == "CIDADE_DE_PROVA").Which;
        ItemDoModelo cidade = modelo.Itens.Should().ContainSingle(static i => i.FatoCodigo == "CIDADE_DE_PROVA").Which;

        secao.Exibicao.Should().BeNull();
        cidade.EtapaCodigo.Should().Be(secao.Codigo);
        cidade.Apresentacao.TipoRenderizacao.Should().Be(TipoRenderizacao.EscolhaUnica);
        cidade.Regras.Exibicao.Should().BeNull();
        cidade.Regras.Obrigatoriedade.Tipo.Should().Be(TipoObrigatoriedade.Sempre);
    }

    [Fact]
    public void Inscricao_SemProva_NaoPedeCidadeDeProva()
    {
        ConteudoDoModelo modelo = ModelosDaSementePsrMedicina2027.Inscricao(comProva: false);

        modelo.Etapas.Should().NotContain(static e => e.Codigo == "CIDADE_DE_PROVA");
        modelo.Itens.Should().NotContain(static i => i.FatoCodigo == "CIDADE_DE_PROVA");
    }

    [Fact]
    public void Inscricao_ComProva_AcrescentaCidadeEAtendimento()
    {
        ConteudoDoModelo semProva = ModelosDaSementePsrMedicina2027.Inscricao(comProva: false);
        ConteudoDoModelo comProva = ModelosDaSementePsrMedicina2027.Inscricao(comProva: true);

        comProva.Etapas.Should().HaveCount(semProva.Etapas.Count + 2);
        comProva.Itens.Should().HaveCount(semProva.Itens.Count + 5);
    }

    [Fact]
    public void ComandosDosModelos_ComProva_IncluiCidadeNaSemente()
    {
        ComandoDaSemente comando = SementePsrMedicina2027.ComandosDosModelos()
            .Single(static c => c.Item!.Codigo == SementePsrMedicina2027.ModeloDeInscricaoComProva);

        comando.Texto.Should().Contain("CIDADE_DE_PROVA");
    }
}
