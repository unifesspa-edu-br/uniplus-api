namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Application.Mappings;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.ConteudosDeModeloParaTeste;
using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.DeclaracoesDeModeloDeTeste;

/// <summary>O conjunto básico no modelo de inscrição gravado antes de o conjunto ganhar itens.</summary>
public sealed class ConjuntoBasicoDoModeloTests
{
    private static readonly Dictionary<string, string> Formatos = new(StringComparer.Ordinal)
    {
        ["NOME"] = "LIVRE",
        ["NOME_SOCIAL"] = "NOME_PESSOA",
        ["CPF"] = "CPF",
        ["RG_NUMERO"] = "LIVRE",
        ["RG_ORGAO_EMISSOR_OUTRO"] = "LIVRE",
        ["PASSAPORTE_NUMERO"] = "LIVRE",
        ["RNM_NUMERO"] = "LIVRE",
        ["NOME_MAE"] = "NOME_PESSOA",
        ["NOME_PAI"] = "NOME_PESSOA",
        ["EMAIL"] = "EMAIL",
        ["TELEFONE"] = "TELEFONE",
        ["NOME_COMUNIDADE"] = "LIVRE",
    };

    private static readonly PaginaDoModelo Dados = new("DADOS", 1, TipoPaginaFormulario.Secao, null, "Dados", null, null, null);

    [Fact(DisplayName = "O modelo de inscrição gravado sem os dados de sexo e gênero os recebe na seção dos dados básicos, sem repetir a ordem de nenhum item")]
    public void Mesclar_ModeloGravadoAntesDosTresDados_RecebeOsItensSemColidir()
    {
        ConteudoDoModelo legado = ModeloGravadoAntesDosTresDados();

        ConteudoDoModelo mesclado = ConjuntoBasicoDoModelo.Mesclar(legado, Formatos);

        mesclado.Itens.Select(static i => i.FatoCodigo).Should().Contain(ConjuntoBasicoDaInscricao.Fatos);
        mesclado.Itens.Select(static i => i.Ordem).Should().OnlyHaveUniqueItems();
        int ultimoDaSecao = mesclado.Itens.Where(static i => i.PaginaCodigo == ConjuntoBasicoDaInscricao.CodigoDaSecao).Max(static i => i.Ordem);
        mesclado.Itens.Single(static i => i.FatoCodigo == "BAIXA_RENDA").Ordem.Should().BeGreaterThan(ultimoDaSecao, "o item do administrador segue depois da seção");
    }

    [Fact(DisplayName = "O modelo com a seção dos dados básicos fora da ordem canônica sai na ordem e com os rótulos do conjunto")]
    public void Mesclar_SecaoForaDaOrdem_SaiNaOrdemCanonica()
    {
        ConteudoDoModelo completo = ConteudoComOConjunto();
        ItemDoModelo[] doConjunto = [.. completo.Itens.Where(i => i.PaginaCodigo == ConjuntoBasicoDaInscricao.CodigoDaSecao)];
        ItemDoModelo sexo = doConjunto.Single(static i => i.FatoCodigo == "SEXO");
        ItemDoModelo[] sexoNoFim = [.. doConjunto.Where(static i => i.FatoCodigo != "SEXO"), sexo.ComRotulo("Sexo")];
        ConteudoDoModelo legado = completo with { Itens = [.. sexoNoFim.Select(static (item, ordem) => item with { Ordem = ordem })] };

        ConteudoDoModelo mesclado = ConjuntoBasicoDoModelo.Mesclar(legado, Formatos);

        mesclado.Itens.OrderBy(static i => i.Ordem).Select(static i => i.FatoCodigo)
            .Should().Equal(ConjuntoBasicoDaInscricao.Itens.Select(static i => i.FatoCodigo));
        mesclado.Itens.Single(static i => i.FatoCodigo == "SEXO").Apresentacao.Rotulo.Should().Be("Sexo biológico");
    }

    [Fact(DisplayName = "O modelo de inscrição, como aplicado, traz o conjunto básico na seção reservada")]
    public void ComoAplicado_ModeloDeInscricao_TrazOConjuntoBasico()
    {
        ModeloFormulario modelo = ModeloGravado(DaInscricao);

        ConteudoDoModelo aplicado = ConjuntoBasicoDoModelo.ComoAplicado(modelo, []);

        aplicado.Itens.Select(static i => i.FatoCodigo).Should().Contain(ConjuntoBasicoDaInscricao.Fatos);
    }

    [Fact(DisplayName = "O modelo facultativo que serve à fase de inscrição, como aplicado, também traz o conjunto básico")]
    public void ComoAplicado_ServeAInscricaoFacultativo_TrazOConjuntoBasico()
    {
        ModeloFormulario modelo = ModeloGravado(new DeclaracaoDoFormulario(ServeAFaseDeInscricao: true, Obrigatorio: false));

        ConteudoDoModelo aplicado = ConjuntoBasicoDoModelo.ComoAplicado(modelo, []);

        aplicado.Itens.Select(static i => i.FatoCodigo).Should().Contain(ConjuntoBasicoDaInscricao.Fatos);
    }

    [Fact(DisplayName = "O modelo de outra fase, como aplicado, é o conteúdo gravado")]
    public void ComoAplicado_OutraFase_DevolveOConteudoGravado()
    {
        ModeloFormulario modelo = ModeloGravado(DeOutraFaseObrigatoria);

        ConteudoDoModelo aplicado = ConjuntoBasicoDoModelo.ComoAplicado(modelo, []);

        aplicado.Should().BeSameAs(modelo.Conteudo);
    }

    [Fact(DisplayName = "Na escrita, o modelo que serve à fase de inscrição recebe a seção dos dados básicos, obrigatório ou facultativo")]
    public void ComASecaoDosDadosBasicos_ServeAInscricao_MesclaOConjunto()
    {
        ConteudoDoModeloInput entrada = new ConteudoDoModelo("Modelo", [Secao, Revisao], [], [], [], []).ParaEntrada();

        (ConteudoDoModeloInput? mesclada, _) = ConjuntoBasicoDoModelo.ComASecaoDosDadosBasicos(
            entrada, new DeclaracaoDoFormulario(ServeAFaseDeInscricao: true, Obrigatorio: false), entrada);

        mesclada!.Itens!.Select(static i => i.FatoCodigo).Should().Contain(ConjuntoBasicoDaInscricao.Fatos);
    }

    [Fact(DisplayName = "Na escrita, o modelo de outra fase não recebe a seção dos dados básicos")]
    public void ComASecaoDosDadosBasicos_OutraFase_DevolveAEntradaSemMudar()
    {
        ConteudoDoModeloInput entrada = new ConteudoDoModelo("Modelo", [Secao, Revisao], [], [], [], []).ParaEntrada();

        (ConteudoDoModeloInput? mesclada, List<FieldError> erros) = ConjuntoBasicoDoModelo.ComASecaoDosDadosBasicos(entrada, DeOutraFaseObrigatoria, entrada);

        mesclada.Should().BeSameAs(entrada);
        erros.Should().BeEmpty();
    }

    [Fact(DisplayName = "Na escrita, a declaração ausente não mescla o conjunto: a recusa dela é do cadastro")]
    public void ComASecaoDosDadosBasicos_SemDeclaracao_DevolveAEntradaSemMudar()
    {
        ConteudoDoModeloInput entrada = new ConteudoDoModelo("Modelo", [Secao, Revisao], [], [], [], []).ParaEntrada();

        (ConteudoDoModeloInput? mesclada, _) = ConjuntoBasicoDoModelo.ComASecaoDosDadosBasicos(entrada, null, entrada);

        mesclada.Should().BeSameAs(entrada);
    }

    [Fact(DisplayName = "O item do cliente na seção dos dados básicos não gera recusa de estrutura no domínio: só a escrita recusa a seção reservada")]
    public void ConferirCriacao_ItemDoClienteNaSecaoReservada_NaoRecusaAEstrutura()
    {
        ItemDoModelo doCliente = ItemOpcionalDeBaixaRenda(0) with { PaginaCodigo = ConjuntoBasicoDaInscricao.CodigoDaSecao };
        ConteudoDoModelo mesclado = ConjuntoBasicoDoModelo.Mesclar(new ConteudoDoModelo("Modelo", [Secao, Revisao], [doCliente], [], [], []), Formatos);

        List<FieldError> erros = ModeloFormulario.ConferirCriacao(
            "MODELO_DO_TESTE", new DescritivoDoModelo("Modelo do teste", null, null), DaInscricao, mesclado,
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal), conteudoPorInteiro: true);

        erros.Select(static e => (e.Field, e.Error.Code)).Should().BeEmpty();
    }

    private static ConteudoDoModelo ConteudoComOConjunto() =>
        ConjuntoBasicoDoModelo.Mesclar(new ConteudoDoModelo("Inscrição", [Dados], [], [], [], []), Formatos);

    private static ConteudoDoModelo ModeloGravadoAntesDosTresDados()
    {
        ConteudoDoModelo completo = ConteudoComOConjunto();
        string[] novos = ["SEXO_CIVIL", "IDENTIDADE_GENERO", "ORIENTACAO_SEXUAL"];
        ItemDoModelo[] doConjunto = [.. completo.Itens.Where(i => i.PaginaCodigo == ConjuntoBasicoDaInscricao.CodigoDaSecao && !novos.Contains(i.FatoCodigo))];
        ItemDoModelo[] gravadosAntes =
        [
            .. doConjunto.Select(static (item, ordem) => item with { Ordem = ordem }),
            ItemOpcionalDeBaixaRenda(doConjunto.Length),
        ];
        return completo with { Itens = gravadosAntes };
    }

    private static ItemDoModelo ItemOpcionalDeBaixaRenda(int ordem) =>
        new("BAIXA_RENDA", ordem, "DADOS", new ApresentacaoDoCampo("Baixa renda", TipoDeCampo.SimOuNao), null, new RegrasDoCampo(null, Obrigatoriedade.Nunca, []));

    private static ModeloFormulario ModeloGravado(DeclaracaoDoFormulario declaracao)
    {
        ConteudoDoModelo conteudo = ConjuntoBasicoDoModelo.Mesclar(new ConteudoDoModelo("Modelo", [Secao, Revisao], [], [], [], []), Formatos);
        Result<ModeloFormulario> criado = ModeloFormulario.Criar(
            "MODELO_DO_TESTE", new DescritivoDoModelo("Modelo do teste", null, null), declaracao, conteudo,
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal));
        criado.IsSuccess.Should().BeTrue(string.Join("; ", criado.Errors.Select(static e => $"{e.Field}: {e.Error.Code}")));
        return criado.Value!;
    }
}
