namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O formulário de inscrição coleta o conjunto básico do candidato numa seção reservada: o envio
/// pode omitir ou repetir os itens dela, nunca alterá-los, e os itens do cliente ficam depois dela.
/// </summary>
public class ConjuntoBasicoDaInscricaoTests : TestesDeAvaliacao
{
    private static readonly int Teto = ConjuntoBasicoDaInscricao.Itens.Count;

    [Theory(DisplayName = "Os itens do cliente sobem acima da seção só quando colidem com ela, e os básicos omitidos entram")]
    [InlineData(0, 22)]
    [InlineData(22, 22)]
    [InlineData(40, 40)]
    public void MesclarItens_ItemDoCliente_SobeSoQuandoColide(int ordemEnviada, int ordemGravada)
    {
        (IReadOnlyList<FatoColetadoInput> itens, _, List<FieldError> erros) = ConjuntoBasicoDaInscricao.MesclarItens(
            [ItemDoCliente("QUILOMBOLA", ordemEnviada)], [], ConjuntoBasicoDaInscricao.Itens);

        erros.Should().BeEmpty();
        itens.Should().HaveCount(Teto + 1);
        itens.Single(static i => i.FatoCodigo == "QUILOMBOLA").Ordem.Should().Be(ordemGravada);
        itens.Where(static i => ConjuntoBasicoDaInscricao.Fatos.Contains(i.FatoCodigo)).Should().Equal(ConjuntoBasicoDaInscricao.Itens);
    }

    [Fact(DisplayName = "O grupo do cliente que colide com a seção sobe com os itens")]
    public void MesclarItens_GrupoQueColide_Sobe()
    {
        GrupoColetadoInput grupo = new("COMPOSICAO", 1, "Composição familiar", "DADOS", 1, null, null, null, null, []);

        (_, IReadOnlyList<GrupoColetadoInput> grupos, _) = ConjuntoBasicoDaInscricao.MesclarItens(
            [ItemDoCliente("QUILOMBOLA", 0)], [grupo], ConjuntoBasicoDaInscricao.Itens);

        grupos.Should().ContainSingle().Which.Ordem.Should().Be(Teto + 1);
    }

    [Fact(DisplayName = "Item e grupo do cliente na seção reservada são recusados, cada um no seu campo")]
    public void MesclarItens_ClienteNaSecaoReservada_Recusa()
    {
        GrupoColetadoInput grupo = new("COMPOSICAO", 30, "Composição familiar", ConjuntoBasicoDaInscricao.CodigoDaSecao, 1, null, null, null, null, []);

        (_, _, List<FieldError> erros) = ConjuntoBasicoDaInscricao.MesclarItens(
            [ItemDoCliente("QUILOMBOLA", 30) with { EtapaCodigo = ConjuntoBasicoDaInscricao.CodigoDaSecao }], [grupo], ConjuntoBasicoDaInscricao.Itens);

        erros.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("itens[0].etapaCodigo", EstruturaFormularioErrorCodes.SecaoReservada),
            ("grupos[0].etapaCodigo", EstruturaFormularioErrorCodes.SecaoReservada),
        ]);
    }

    [Fact(DisplayName = "O item básico repetido como está é aceito, mesmo com espaços e listas vazias")]
    public void MesclarItens_BasicoRepetidoComoEsta_Aceita()
    {
        FatoColetadoInput nome = ConjuntoBasicoDaInscricao.Itens[0];

        (IReadOnlyList<FatoColetadoInput> itens, _, List<FieldError> erros) = ConjuntoBasicoDaInscricao.MesclarItens(
            [nome with { Rotulo = $" {nome.Rotulo} ", Precondicao = [], Restricoes = [] }], [], ConjuntoBasicoDaInscricao.Itens);

        erros.Should().BeEmpty();
        itens.Should().HaveCount(Teto);
    }

    [Theory(DisplayName = "O item básico alterado é recusado na posição do envio")]
    [InlineData("rotulo")]
    [InlineData("obrigatoriedade")]
    [InlineData("secao")]
    [InlineData("ordem")]
    public void MesclarItens_BasicoAlterado_Recusa(string alteracao)
    {
        FatoColetadoInput nome = ConjuntoBasicoDaInscricao.Itens[0];
        FatoColetadoInput alterado = alteracao switch
        {
            "rotulo" => nome with { Rotulo = "Nome" },
            "obrigatoriedade" => nome with { Obrigatoriedade = PredicadoDnfJson.ObrigatoriedadeNunca },
            "secao" => nome with { EtapaCodigo = "DADOS" },
            _ => nome with { Ordem = 40 },
        };

        (_, _, List<FieldError> erros) = ConjuntoBasicoDaInscricao.MesclarItens(
            [ItemDoCliente("QUILOMBOLA", 30), alterado], [], ConjuntoBasicoDaInscricao.Itens);

        erros.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Field = "itens[1]",
            Error = new { Code = EstruturaFormularioErrorCodes.DadoBasicoAlterado },
        });
    }

    [Fact(DisplayName = "O item básico com condição sem valor é recusado como alteração, sem erro")]
    public void MesclarItens_BasicoComCondicaoSemValor_Recusa()
    {
        FatoColetadoInput nomeSocial = ConjuntoBasicoDaInscricao.Itens.Single(static i => i.FatoCodigo == "NOME_SOCIAL");

        (_, _, List<FieldError> erros) = ConjuntoBasicoDaInscricao.MesclarItens(
            [nomeSocial with { Precondicao = [[new CondicaoPrecondicaoInput("DESEJA_NOME_SOCIAL", "IGUAL", default)]] }], [], ConjuntoBasicoDaInscricao.Itens);

        erros.Should().ContainSingle().Which.Error.Code.Should().Be(EstruturaFormularioErrorCodes.DadoBasicoAlterado);
    }

    [Fact(DisplayName = "A referência é o item gravado na seção; o básico gravado fora dela não é referência")]
    public void Referencia_SoOGravadoNaSecao()
    {
        FatoColetadoInput nomeNaSecao = ConjuntoBasicoDaInscricao.Itens[0] with { Rotulo = "Nome civil" };
        FatoColetadoInput sexoForaDaSecao = ConjuntoBasicoDaInscricao.Itens.Single(static i => i.FatoCodigo == "SEXO") with { EtapaCodigo = "DADOS" };

        IReadOnlyList<FatoColetadoInput> referencia = ConjuntoBasicoDaInscricao.Referencia([nomeNaSecao, sexoForaDaSecao]);

        referencia.Should().HaveCount(Teto);
        referencia[0].Rotulo.Should().Be("Nome civil");
        referencia.Single(static i => i.FatoCodigo == "SEXO").EtapaCodigo.Should().Be(ConjuntoBasicoDaInscricao.CodigoDaSecao);
    }

    [Theory(DisplayName = "A seção omitida entra na primeira posição, e as etapas do cliente sobem só quando colidem")]
    [InlineData(0, 1)]
    [InlineData(3, 3)]
    public void MesclarEtapas_SecaoOmitida_Entra(int ordemEnviada, int ordemGravada)
    {
        EtapaFormularioInput dados = new("DADOS", ordemEnviada, EstruturaFormulario.TipoSecao, null, "Dados", null, null);

        (IReadOnlyList<EtapaFormularioInput> etapas, List<FieldError> erros) = ConjuntoBasicoDaInscricao.MesclarEtapas([dados], ConjuntoBasicoDaInscricao.Secao);

        erros.Should().BeEmpty();
        etapas.Should().Contain(ConjuntoBasicoDaInscricao.Secao);
        etapas.Single(static e => e.Codigo == "DADOS").Ordem.Should().Be(ordemGravada);
    }

    [Theory(DisplayName = "A seção enviada igual à gravada é aceita; alterada, é recusada")]
    [InlineData(false)]
    [InlineData(true)]
    public void MesclarEtapas_SecaoEnviada_SoComoEsta(bool alterada)
    {
        EtapaFormularioInput secao = alterada ? ConjuntoBasicoDaInscricao.Secao with { Titulo = "Identificação" } : ConjuntoBasicoDaInscricao.Secao;

        (_, List<FieldError> erros) = ConjuntoBasicoDaInscricao.MesclarEtapas([secao], ConjuntoBasicoDaInscricao.Secao);

        erros.Select(static e => e.Error.Code).Should().Equal(alterada ? [EstruturaFormularioErrorCodes.SecaoReservadaAlterada] : []);
    }

    [Theory(DisplayName = "O estrangeiro não vê RG nem naturalidade e tem CPF opcional; quem não é estrangeiro, o contrário")]
    [InlineData("ESTRANGEIRO", Ternario.Falso, Ternario.Verdadeiro, Ternario.Falso)]
    [InlineData("NATO", Ternario.Verdadeiro, Ternario.Falso, Ternario.Verdadeiro)]
    [InlineData("NATURALIZADO", Ternario.Verdadeiro, Ternario.Falso, Ternario.Verdadeiro)]
    public void Avaliar_PelaNacionalidade(string nacionalidade, Ternario rgVisivel, Ternario passaporteVisivel, Ternario cpfObrigatorio)
    {
        AvaliacaoFormulario avaliacao = Avaliar(("NACIONALIDADE", nacionalidade));

        Item(avaliacao, "RG_NUMERO").Visivel.Should().Be(rgVisivel);
        Item(avaliacao, "NATURALIDADE_MUNICIPIO").Visivel.Should().Be(rgVisivel);
        Item(avaliacao, "DOCUMENTO_ESTRANGEIRO_NUMERO").Visivel.Should().Be(passaporteVisivel);
        Item(avaliacao, "CPF").Visivel.Should().Be(Ternario.Verdadeiro);
        Item(avaliacao, "CPF").Obrigatorio.Should().Be(cpfObrigatorio);
    }

    [Theory(DisplayName = "O nome social só aparece para quem deseja usá-lo, e o nome do pai é opcional")]
    [InlineData(true, Ternario.Verdadeiro)]
    [InlineData(false, Ternario.Falso)]
    public void Avaliar_NomeSocialPelaPergunta(bool deseja, Ternario visivel)
    {
        AvaliacaoFormulario avaliacao = Avaliar(("DESEJA_NOME_SOCIAL", deseja));

        Item(avaliacao, "NOME_SOCIAL").Visivel.Should().Be(visivel);
        Item(avaliacao, "NOME_PAI").Obrigatorio.Should().Be(Ternario.Falso);
    }

    private static FatoColetadoInput ItemDoCliente(string fato, int ordem) =>
        new(fato, ordem, fato, "BOOLEANO", PredicadoDnfJson.ObrigatoriedadeSempre, null, "DADOS");

    private static AvaliacaoItem Item(AvaliacaoFormulario avaliacao, string fato) => avaliacao.Itens.Single(i => i.FatoCodigo == fato);

    /// <summary>Avalia a seção como a constante a declara, lida pelo mesmo caminho da escrita.</summary>
    private AvaliacaoFormulario Avaliar(params (string Fato, object Valor)[] respostas)
    {
        DefinicaoItem[] itens = [.. ConjuntoBasicoDaInscricao.Itens.Select(static item =>
        {
            PredicadoDnf? obrigatoriedade = EntradaDeRegras.Predicado(item.PredicadoObrigatoriedade).Value;
            return new DefinicaoItem(
                item.FatoCodigo,
                EntradaDeRegras.Predicado(item.Precondicao).Value,
                EntradaDeRegras.Obrigatoriedade(item.Obrigatoriedade, obrigatoriedade)!,
                [.. (item.Restricoes ?? []).Select(static r => EntradaDeRegras.Restricao(r).Value!)]);
        })];
        DefinicaoFormulario definicao = new([new DefinicaoEtapa(ConjuntoBasicoDaInscricao.CodigoDaSecao, exibicao: null, itens)], termos: [], derivacoes: []);

        return AvaliarDefinicao(definicao, new EntradaAvaliacaoFormulario(
            respostas.ToDictionary(static r => r.Fato, static r => JsonSerializer.SerializeToElement(r.Valor), StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal),
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal)));
    }
}
