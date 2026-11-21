namespace Unifesspa.UniPlus.Regras.UnitTests.Formularios;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// A montagem das definições, das entradas e das condições que os testes da avaliação do formulário
/// repetem: uma etapa de dados, com os itens e as respostas dadas.
/// </summary>
internal static class ApoioDaAvaliacaoDoFormulario
{
    internal const string Etapa = "DADOS";

    internal static DefinicaoFormulario Formulario(params DefinicaoItem[] itens) =>
        new([new DefinicaoEtapa(Etapa, exibicao: null, itens)], termos: [], derivacoes: []);

    internal static DefinicaoItem Item(
        string fato,
        PredicadoDnf? exibicao = null,
        Obrigatoriedade? obrigatoriedade = null,
        IReadOnlyList<RestricaoValor>? restricoes = null) =>
        new(fato, exibicao, obrigatoriedade ?? Obrigatoriedade.Sempre, restricoes ?? []);

    internal static EntradaAvaliacaoFormulario Entrada(bool etapaConcluida, params (string Fato, object Valor)[] respostas) =>
        new(
            Respostas(respostas),
            etapaConcluida ? new HashSet<string>([Etapa], StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal),
            new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
            Envio: null);

    internal static Dictionary<string, JsonElement> Respostas((string Fato, object Valor)[] respostas) =>
        respostas.ToDictionary(
            static r => r.Fato,
            static r => r.Valor is JsonElement json ? json : JsonSerializer.SerializeToElement(r.Valor),
            StringComparer.Ordinal);

    internal static CondicaoDnf Condicao(string fato, Operador operador, object valor) =>
        CondicaoDnf.Criar(
            fato,
            operador,
            valor is JsonElement json ? json : JsonSerializer.SerializeToElement(valor)).Value!;

    internal static PredicadoDnf Predicado(params CondicaoDnf[] condicoes) =>
        PredicadoDnf.CriarDeCondicoesAgrupadas([.. condicoes.Select(static c => (0, c))]).Value!;

    internal static DerivacaoDoFato Derivacao(string fato, PredicadoDnf quando, string contribui)
    {
        RegraDerivacao regra = RegraDerivacao.Criar(quando, contribui).Value!;
        return DerivacaoDoFato.Criar(fato, [regra], regra.FatosCitados, [contribui]).Value!;
    }
}
