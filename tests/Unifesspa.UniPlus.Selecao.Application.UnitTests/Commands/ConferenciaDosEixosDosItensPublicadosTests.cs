namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FatoCitadoDeTeste;

public sealed class ConferenciaDosEixosDosItensPublicadosTests
{
    [Theory(DisplayName = "Item e campo já coletados recusam a troca do tipo ou da proteção, identificando fato, eixo e saída")]
    [InlineData(false, "tipo de renderização")]
    [InlineData(false, "classificação de proteção")]
    [InlineData(true, "tipo de renderização")]
    [InlineData(true, "classificação de proteção")]
    public void Conferir_ItemOuCampoComEixoTrocado_Recusa(bool campo, string eixo)
    {
        FatosDasVersoesPublicadas publicadas = PublicadasNoLugar(campo, Coletado());
        FatoColetado trocado = eixo == "tipo de renderização" ? Coletado(TipoRenderizacao.Numero) : Coletado(classificacao: "IDENTIFICADOR");
        string caminho = campo ? "formularios[0].grupos[0].subitens[0]" : "formularios[0].itens[0]";

        IReadOnlyList<FieldError> causas = Conferir(publicadas, [new(trocado, caminho)]);

        FieldError causa = causas.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be(CodigoDaRecusaDoEixoAlterado);
        causa.Error.Message.Should().Contain(Fato).And.Contain(eixo).And.Contain("processo novo");
        causa.Field.Should().Be(caminho);
    }

    [Fact(DisplayName = "Trocar juntos o rótulo e a ajuda do item coletado mantém os eixos e é aceito")]
    public void Conferir_RotuloEAjudaAlterados_NaoRecusa()
    {
        FatoColetado publicado = Coletado(ajuda: "Ajuda original");
        FatoColetado atual = Coletado(rotulo: "Outro rótulo", ajuda: "Outra ajuda");
        FatosDasVersoesPublicadas publicadas = Publicadas([publicado]);

        Conferir(publicadas, [new(atual, "formularios[0].itens[0]")]).Should().BeEmpty();

        atual.Rotulo.Should().NotBe(publicado.Rotulo);
        atual.Ajuda.Should().NotBe(publicado.Ajuda);
    }

    [Fact(DisplayName = "O fato ainda não coletado entra com tipo e proteção diferentes dos fatos publicados")]
    public void Conferir_FatoNovoComOutrosEixos_NaoRecusa()
    {
        FatosDasVersoesPublicadas publicadas = Publicadas([Coletado()]);
        FatoColetado novo = Coletado(TipoRenderizacao.Numero, "IDENTIFICADOR", fato: FatoNovo);

        publicadas.EixosDosItens.Should().NotContainKey(novo.FatoCodigo);
        Conferir(publicadas, [new(Coletado(), "formularios[0].itens[0]"), new(novo, "formularios[0].itens[1]")]).Should().BeEmpty();
    }

    [Fact(DisplayName = "A comparação com o envelope publicado identifica o item e o tipo trocado")]
    public void Conferir_ItemDivergenteDoEnvelope_RecusaComCodigoECaminho()
    {
        FatosDasVersoesPublicadas publicadas = Publicadas([Item(TipoRenderizacao.Data)]);

        IReadOnlyList<FieldError> causas = Conferir(publicadas, [new(Item(TipoRenderizacao.SimOuNao), "formularios[0].itens[3]")]);

        FieldError causa = causas.Should().ContainSingle().Subject;
        causa.Error.Code.Should().Be(CodigoDaRecusaDoEixoAlterado);
        causa.Error.Message.Should().Contain(Fato).And.Contain("tipo de renderização");
        causa.Field.Should().Be("formularios[0].itens[3]");
    }

    [Fact(DisplayName = "A comparação aceita os mesmos eixos e um fato novo")]
    public void Conferir_MesmosEixosEFatoNovo_NaoRecusa()
    {
        FatosDasVersoesPublicadas publicadas = Publicadas([Item(TipoRenderizacao.SimOuNao)]);

        Conferir(publicadas, [new(Item(TipoRenderizacao.SimOuNao), "formularios[0].itens[0]"), new(ItemNovo(), "formularios[0].itens[1]")])
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "O campo de grupo é comparado contra o que o grupo publicou")]
    public void Conferir_CampoDeGrupoDivergente_RecusaNoCampo()
    {
        GrupoColetado grupo = GrupoColetadoDeTeste.Agrupando("GRUPO", 0)
            .ComRotulo("Grupo")
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ComCampos(Item(TipoRenderizacao.Data))
            .DaFinalidade(FinalidadeFormulario.Inscricao)
            .Construir();
        FatosDasVersoesPublicadas publicadas = VersoesPublicadasDeTeste.SemEixosCongelados([VersoesPublicadasDeTeste.Versao([], [], [grupo])]);

        IReadOnlyList<FieldError> causas = Conferir(publicadas, [new(Item(TipoRenderizacao.SimOuNao), "formularios[0].grupos[0].subitens[2]")]);

        causas.Should().ContainSingle().Which.Field.Should().Be("formularios[0].grupos[0].subitens[2]");
        causas[0].Error.Code.Should().Be(CodigoDaRecusaDoEixoAlterado);
    }

    [Fact(DisplayName = "Versões distintas geram uma recusa por ocorrência na ordem recebida de cada formulário")]
    public void Conferir_VariasOcorrenciasEVersoes_ConservaOrdemECaminhos()
    {
        FatosDasVersoesPublicadas publicadas = Publicadas([Item(TipoRenderizacao.Data)], [Item(TipoRenderizacao.SimOuNao)]);
        string[] caminhos = ["formularios[2].itens[9]", "formularios[2].grupos[1].subitens[4]", "formularios[0].itens[3]"];

        IReadOnlyList<FieldError> causas = Conferir(publicadas, caminhos.Select(caminho => new ItemNoDocumento(Item(TipoRenderizacao.SimOuNao), caminho)));

        causas.Select(static c => c.Field).Should().Equal(caminhos);
        causas.Select(static c => c.Error.Code).Should().AllBe(CodigoDaRecusaDoEixoAlterado);
    }

    private static IReadOnlyList<FieldError> Conferir(FatosDasVersoesPublicadas publicadas, IEnumerable<ItemNoDocumento> itens) =>
        ConferenciaDosEixosDosFatosColetados.Conferir(publicadas, itens, null, []);

    private static FatosDasVersoesPublicadas Publicadas(params FatoColetado[][] versoes) =>
        VersoesPublicadasDeTeste.SemEixosCongelados([.. versoes.Select(static itens => VersoesPublicadasDeTeste.Versao([], itens))]);

    private static FatosDasVersoesPublicadas PublicadasNoLugar(bool campo, FatoColetado publicado)
    {
        if (!campo)
        {
            return Publicadas([publicado]);
        }

        Result<GrupoColetado> grupo = GrupoColetadoDeTeste.Agrupando("GRUPO", 0)
            .ComRotulo("Grupo")
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ComCampos(publicado)
            .DaFinalidade(FinalidadeFormulario.Inscricao)
            .Criar();
        grupo.IsSuccess.Should().BeTrue(grupo.Error?.Message);
        return VersoesPublicadasDeTeste.SemEixosCongelados([VersoesPublicadasDeTeste.Versao([], [], [grupo.Value!])]);
    }

    private static FatoColetado Coletado(
        TipoRenderizacao tipo = TipoRenderizacao.SimOuNao, string classificacao = "PESSOAL", string rotulo = "Tem renda?", string? ajuda = null, string fato = Fato) =>
        FatoColetadoDeTeste.Coletando(fato, 0)
            .ComRotulo(rotulo)
            .DoTipo(tipo)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ComClassificacao(classificacao)
            .ComAjuda(ajuda)
            .Construir();
}
