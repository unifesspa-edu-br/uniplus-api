namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;

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

    private static readonly EtapaDoModelo Dados = new("DADOS", 1, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null, null);

    [Fact(DisplayName = "O modelo de inscrição gravado sem os dados de sexo e gênero os recebe na seção dos dados básicos, sem repetir a ordem de nenhum item")]
    public void Mesclar_ModeloGravadoAntesDosTresDados_RecebeOsItensSemColidir()
    {
        ConteudoDoModelo completo = ConjuntoBasicoDoModelo.Mesclar(
            new ConteudoDoModelo("Inscrição", [Dados], [], [], [], []), Formatos);
        string[] novos = ["SEXO_CIVIL", "IDENTIDADE_GENERO", "ORIENTACAO_SEXUAL"];
        ItemDoModelo[] doConjunto = [.. completo.Itens.Where(i => i.EtapaCodigo == ConjuntoBasicoDaInscricao.CodigoDaSecao && !novos.Contains(i.FatoCodigo))];
        ItemDoModelo[] gravadosAntes =
        [
            .. doConjunto.Select(static (item, ordem) => item with { Ordem = ordem }),
            new ItemDoModelo("BAIXA_RENDA", doConjunto.Length, "DADOS", "Baixa renda", TipoRenderizacao.Booleano, null, null, Obrigatoriedade.Nunca, null, [], false),
        ];
        ConteudoDoModelo legado = completo with { Itens = gravadosAntes };

        ConteudoDoModelo mesclado = ConjuntoBasicoDoModelo.Mesclar(legado, Formatos);

        mesclado.Itens.Select(static i => i.FatoCodigo).Should().Contain(ConjuntoBasicoDaInscricao.Fatos);
        mesclado.Itens.Select(static i => i.Ordem).Should().OnlyHaveUniqueItems();
        int ultimoDaSecao = mesclado.Itens.Where(static i => i.EtapaCodigo == ConjuntoBasicoDaInscricao.CodigoDaSecao).Max(static i => i.Ordem);
        mesclado.Itens.Single(static i => i.FatoCodigo == "BAIXA_RENDA").Ordem.Should().BeGreaterThan(ultimoDaSecao, "o item do administrador segue depois da seção");
    }

    [Fact(DisplayName = "O modelo com a seção dos dados básicos fora da ordem canônica sai na ordem e com os rótulos do conjunto")]
    public void Mesclar_SecaoForaDaOrdem_SaiNaOrdemCanonica()
    {
        ConteudoDoModelo completo = ConjuntoBasicoDoModelo.Mesclar(
            new ConteudoDoModelo("Inscrição", [Dados], [], [], [], []), Formatos);
        ItemDoModelo[] doConjunto = [.. completo.Itens.Where(i => i.EtapaCodigo == ConjuntoBasicoDaInscricao.CodigoDaSecao)];
        ItemDoModelo sexo = doConjunto.Single(static i => i.FatoCodigo == "SEXO");
        ItemDoModelo[] sexoNoFim = [.. doConjunto.Where(static i => i.FatoCodigo != "SEXO"), sexo with { Rotulo = "Sexo" }];
        ConteudoDoModelo legado = completo with { Itens = [.. sexoNoFim.Select(static (item, ordem) => item with { Ordem = ordem })] };

        ConteudoDoModelo mesclado = ConjuntoBasicoDoModelo.Mesclar(legado, Formatos);

        mesclado.Itens.OrderBy(static i => i.Ordem).Select(static i => i.FatoCodigo)
            .Should().Equal(ConjuntoBasicoDaInscricao.Itens.Select(static i => i.FatoCodigo));
        mesclado.Itens.Single(static i => i.FatoCodigo == "SEXO").Rotulo.Should().Be("Sexo biológico");
    }
}
