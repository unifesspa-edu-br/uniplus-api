namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using System.Text.Json;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Montagens comuns dos testes do modelo de formulário: páginas, itens, conteúdo e criação do modelo.
/// </summary>
internal static class ApoioDoModeloDeFormulario
{
    internal static readonly Dictionary<string, IReadOnlyCollection<string>> SemDerivacoes = new(StringComparer.Ordinal);

    internal static readonly PaginaDoModelo Dados = new("DADOS", 0, TipoPaginaFormulario.Secao, null, "Dados", null, null, null);
    internal static readonly PaginaDoModelo Revisao = new("REVISAO", 1, TipoPaginaFormulario.Bloco, BlocoSistema.RevisaoEAceite, "Revisão", null, null, null);

    internal static PredicadoDnf Quando(string fato, Operador operador = Operador.Igual) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([(0, CondicaoDnf.Criar(fato, operador, JsonSerializer.SerializeToElement(true)).Value!)]).Value!;

    internal static ItemDoModelo Item(string fato, int ordem, Obrigatoriedade? obrigatoriedade = null, PredicadoDnf? exibicao = null, string? pagina = "DADOS") =>
        new(
            fato,
            ordem,
            pagina,
            new ApresentacaoDoCampo(fato, TipoDeCampo.SimOuNao),
            null,
            new RegrasDoCampo(exibicao, obrigatoriedade ?? Obrigatoriedade.Sempre, []));

    internal static ConteudoDoModelo Conteudo(IReadOnlyList<ItemDoModelo> itens, params string[] pressupostos) =>
        new("Habilitação", [Dados, Revisao], itens, [], pressupostos, []);

    internal static Result<ModeloFormulario> Criar(
        DeclaracaoDoFormulario? declaracao, ConteudoDoModelo conteudo, IReadOnlyDictionary<string, IReadOnlyCollection<string>>? derivacoes = null) =>
        ModeloFormulario.Criar("HABILITACAO_MEDICINA", new DescritivoDoModelo("Habilitação Medicina 2027", null, "REGULAR_UNIFICADO"), declaracao, conteudo, derivacoes ?? SemDerivacoes);

    internal static Result<ModeloFormulario> CriarInscricao(ConteudoDoModelo conteudo, IReadOnlyDictionary<string, IReadOnlyCollection<string>> derivacoes) =>
        ModeloFormulario.Criar("INSCRICAO_MEDICINA", new DescritivoDoModelo("Inscrição Medicina 2027", null, null), DeclaracaoDoFormulario.DaInscricao, conteudo, derivacoes);

    internal static GrupoDoModelo Composicao(params ItemDoModelo[] campos) =>
        new("COMPOSICAO_FAMILIAR", 1, "DADOS", "Composição familiar", new OcorrenciasDoGrupo(1, 10, IncluiCandidato: false),
            new ExibicaoEObrigatoriedade(null, Obrigatoriedade.Sempre), campos);

    internal static ItemDoModelo Campo(string fato, int ordem, Obrigatoriedade? obrigatoriedade = null, PredicadoDnf? exibicao = null) =>
        Item(fato, ordem, obrigatoriedade, exibicao, pagina: null);
}
