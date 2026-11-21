namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.TestSupport;

using System.Text.Json;

using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

internal static class ConteudosDoModeloDeTeste
{
    public static readonly ConteudoDoModelo Vazio = new(null, [], [], [], [], []);

    public static PredicadoDnf Quando(string fato, Operador operador = Operador.Igual) =>
        Quando(fato, operador, JsonSerializer.SerializeToElement(true));

    public static PredicadoDnf Quando(string fato, Operador operador, JsonElement valor) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, operador, valor).Value!)]).Value!;

    public static EtapaDoModelo Etapa(string codigo = "DADOS", int ordem = 0, PredicadoDnf? exibicao = null) =>
        new(codigo, ordem, TipoEtapaFormulario.Secao, BlocoSistema.Nenhum, "Dados", null, null, exibicao);

    public static ItemDoModelo Item(string fato, int ordem = 0, PredicadoDnf? exibicao = null) =>
        new(fato, ordem, "DADOS", new ApresentacaoDoCampo(fato, TipoRenderizacao.Booleano), null, new RegrasDoCampo(exibicao, Obrigatoriedade.Sempre, []));

    public static GrupoDoModelo Grupo(string codigo, int ordem = 0, PredicadoDnf? exibicao = null, params ItemDoModelo[] campos) =>
        new(codigo, ordem, "DADOS", "Grupo", 1, 10, exibicao, Obrigatoriedade.Sempre, campos);

    public static TermoDoModelo Termo(string codigo, int ordem = 0, PredicadoDnf? exibicao = null) =>
        new(codigo, ordem, Guid.NewGuid(), Guid.NewGuid(), exibicao, Obrigatoriedade.Sempre);

    public static DocumentoExigidoDoModelo Documento(string tipo, PredicadoDnf? quando = null, ExigenciaPorOpcao? porOpcao = null) =>
        new("INSCRICAO", tipo, false, quando, "REMOVE_VANTAGEM", [], porOpcao);
}
