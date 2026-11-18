namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using LinhaDoPredicado = (int Clausula, string Fato, Unifesspa.UniPlus.Regras.Enums.Operador Operador, System.Text.Json.JsonElement Valor);

/// <summary>
/// A regra de homologação das inscrições que a fase declara, no envelope: o resultado apontado e as alternativas de
/// condições na ordem declarada, que decide a ordem dos motivos da inscrição não homologada e por isso não se ordena.
/// O que está ausente é <c>null</c> explícito (a regra da fase, o resultado, o motivo e o enquadramento), como no resto
/// do envelope; o enquadramento usa a forma canônica do predicado, que ordena as cláusulas porque a ordem delas não
/// muda o sentido.
/// </summary>
/// <remarks>
/// A leitura remonta a regra pelas mesmas fábricas do domínio e recusa, como envelope malformado, a forma que a escrita
/// nunca produz: chave ausente ou desconhecida, texto vazio e valor que o domínio não declararia.
/// </remarks>
internal static class RegraDeHomologacaoNoEnvelope
{
    internal const string Chave = "regraDeHomologacao";

    private const string ChaveDoResultado = "resultadoAtoCodigo";
    private const string ChaveDasAlternativas = "alternativas";
    private const string ChaveDasCondicoes = "condicoes";
    private const string ChaveDoTipo = "tipo";
    private const string ChaveDaEdicaoDoExame = "edicaoDoExame";
    private const string ChaveDoMotivoDesfavoravel = "motivoDesfavoravelCodigo";
    private const string ChaveDoEnquadramento = "enquadramento";

    internal static JsonObject Serializar(RegraDeHomologacaoDasInscricoes regra)
    {
        return new JsonObject
        {
            [ChaveDoResultado] = regra.ResultadoAtoCodigo is { } resultado ? HashCanonicalComputer.NormalizeNfc(resultado) : null,
            [ChaveDasAlternativas] = new JsonArray([.. regra.Alternativas.Select(static alternativa => (JsonNode)SerializarAlternativa(alternativa))]),
        };
    }

    private static JsonObject SerializarAlternativa(AlternativaDeCondicoes alternativa) => new()
    {
        [ChaveDasCondicoes] = new JsonArray([.. alternativa.Condicoes.Select(static condicao => (JsonNode)SerializarCondicao(condicao))]),
    };

    private static JsonObject SerializarCondicao(CondicaoDeHomologacao condicao) => new()
    {
        [ChaveDoTipo] = condicao.Tipo.Codigo(),
        [ChaveDaEdicaoDoExame] = condicao.EdicaoDoExame,
        [ChaveDoMotivoDesfavoravel] = condicao.MotivoDesfavoravelCodigo is { } motivo ? HashCanonicalComputer.NormalizeNfc(motivo) : null,
        [ChaveDoEnquadramento] = condicao.Enquadramento is { } enquadramento
            ? SerializacaoDosPredicados.SerializarDnf(SerializacaoDosPredicados.LinhasDoPredicado(enquadramento))
            : null,
    };

    /// <summary>A regra da fase, ou <see langword="null"/> quando a fase não homologa as inscrições.</summary>
    internal static RegraDeHomologacaoDasInscricoes? Ler(LeitorEnvelope leitor, JsonObject fase, string pathDaFase)
    {
        JsonObject? bloco = leitor.ObjetoOpcional(fase, Chave, pathDaFase);
        if (bloco is null || leitor.Falhou)
        {
            return null;
        }

        string path = $"{pathDaFase}.{Chave}";
        leitor.ExigirChaves(bloco, path, ChaveDoResultado, ChaveDasAlternativas);
        string? resultado = TextoNuloOuNaoVazio(leitor, bloco, ChaveDoResultado, path);
        IReadOnlyList<AlternativaDeCondicoes> alternativas = LerAlternativas(leitor, bloco, path);

        return leitor.Falhou ? null : RegraDeHomologacaoDasInscricoes.Declarar(resultado, alternativas);
    }

    private static List<AlternativaDeCondicoes> LerAlternativas(LeitorEnvelope leitor, JsonObject regra, string path)
    {
        JsonArray array = leitor.Array(regra, ChaveDasAlternativas, path);
        string pathLista = $"{path}.{ChaveDasAlternativas}";
        List<AlternativaDeCondicoes> alternativas = [];
        for (int i = 0; !leitor.Falhou && i < array.Count; i++)
        {
            AlternativaDeCondicoes? alternativa = LerAlternativa(leitor, leitor.ItemObjeto(array, i, pathLista), $"{pathLista}[{i}]");
            if (alternativa is not null)
            {
                alternativas.Add(alternativa);
            }
        }

        return alternativas;
    }

    private static AlternativaDeCondicoes? LerAlternativa(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, ChaveDasCondicoes);
        JsonArray array = leitor.Array(item, ChaveDasCondicoes, path);
        string pathLista = $"{path}.{ChaveDasCondicoes}";
        List<CondicaoDeHomologacao> condicoes = [];
        for (int i = 0; !leitor.Falhou && i < array.Count; i++)
        {
            CondicaoDeHomologacao? condicao = LerCondicao(leitor, leitor.ItemObjeto(array, i, pathLista), $"{pathLista}[{i}]");
            if (condicao is not null)
            {
                condicoes.Add(condicao);
            }
        }

        return leitor.Falhou ? null : Declarada(leitor, AlternativaDeCondicoes.Declarar(condicoes), path);
    }

    private static CondicaoDeHomologacao? LerCondicao(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, ChaveDoTipo, ChaveDaEdicaoDoExame, ChaveDoMotivoDesfavoravel, ChaveDoEnquadramento);
        string tipo = leitor.TextoNaoVazio(item, ChaveDoTipo, path);
        int edicaoDoExame = leitor.Inteiro(item, ChaveDaEdicaoDoExame, path);
        string? motivo = TextoNuloOuNaoVazio(leitor, item, ChaveDoMotivoDesfavoravel, path);
        PredicadoDnf? enquadramento = LerEnquadramento(leitor, item, path);

        return leitor.Falhou
            ? null
            : Declarada(leitor, CondicaoDeHomologacao.Declarar(tipo, edicaoDoExame, motivo, enquadramento), path);
    }

    private static PredicadoDnf? LerEnquadramento(LeitorEnvelope leitor, JsonObject condicao, string path)
    {
        if (leitor.Falhou)
        {
            return null;
        }

        IReadOnlyList<LinhaDoPredicado> linhas = LeituraDasCondicoes.LerDnf(leitor, condicao, ChaveDoEnquadramento, path);
        return leitor.Falhou ? null : Predicado(leitor, linhas, $"{path}.{ChaveDoEnquadramento}");
    }

    private static PredicadoDnf? Predicado(LeitorEnvelope leitor, IReadOnlyList<LinhaDoPredicado> linhas, string path)
    {
        Result<PredicadoDnf?> predicado = LeituraDasCondicoes.PredicadoOpcional(linhas);
        return predicado.IsSuccess ? predicado.Value : leitor.Malformado<PredicadoDnf?>(path, predicado.Error!.Message);
    }

    private static string? TextoNuloOuNaoVazio(LeitorEnvelope leitor, JsonObject objeto, string chave, string path) =>
        !leitor.Falhou && objeto[chave] is not null ? leitor.TextoNaoVazio(objeto, chave, path) : null;

    private static T? Declarada<T>(LeitorEnvelope leitor, Result<T> declaracao, string path)
        where T : class
    {
        if (declaracao.IsSuccess)
        {
            return declaracao.Value;
        }

        FieldError recusa = declaracao.Errors[0];
        string pathDaRecusa = recusa.Field is { Length: > 0 } campo ? $"{path}.{campo}" : path;
        return leitor.Malformado<T?>(pathDaRecusa, recusa.Error.Message);
    }
}
