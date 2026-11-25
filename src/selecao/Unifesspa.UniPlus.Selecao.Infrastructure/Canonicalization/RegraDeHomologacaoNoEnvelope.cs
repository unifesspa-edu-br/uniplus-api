namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
/// A leitura recusa como configuração malformada a forma que a escrita nunca produz. As recusas do domínio são
/// repassadas como estão, com código, campo e caminho.
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

    private static JsonObject SerializarCondicao(CondicaoDeHomologacao condicao)
    {
        JsonObject objeto = CamposDaCondicao(condicao);
        ContextoDosRotulos.Acrescentar(objeto, ChaveDoTipo, condicao.Tipo);
        return objeto;
    }

    private static JsonObject CamposDaCondicao(CondicaoDeHomologacao condicao) => new()
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
        List<CondicaoDeHomologacao> condicoes = LerCondicoes(leitor, array, $"{path}.{ChaveDasCondicoes}");
        if (leitor.Falhou)
        {
            return null;
        }

        Result<AlternativaDeCondicoes> declaracao = AlternativaDeCondicoes.Declarar(condicoes);
        return declaracao.IsSuccess ? declaracao.Value : leitor.Propagar<AlternativaDeCondicoes?>(declaracao.Errors, path);
    }

    private static List<CondicaoDeHomologacao> LerCondicoes(LeitorEnvelope leitor, JsonArray array, string pathLista)
    {
        List<CondicaoDeHomologacao> condicoes = [];
        for (int i = 0; !leitor.Falhou && i < array.Count; i++)
        {
            CondicaoDeHomologacao? condicao = LerCondicao(leitor, leitor.ItemObjeto(array, i, pathLista), $"{pathLista}[{i}]");
            if (condicao is not null)
            {
                condicoes.Add(condicao);
            }
        }

        return condicoes;
    }

    private static CondicaoDeHomologacao? LerCondicao(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, ChaveDoTipo, ChaveDaEdicaoDoExame, ChaveDoMotivoDesfavoravel, ChaveDoEnquadramento);
        string tipo = leitor.TextoNaoVazio(item, ChaveDoTipo, path);
        int edicaoDoExame = leitor.Inteiro(item, ChaveDaEdicaoDoExame, path);
        string? motivo = TextoNuloOuNaoVazio(leitor, item, ChaveDoMotivoDesfavoravel, path);
        IReadOnlyList<LinhaDoPredicado> linhas = LerEnquadramento(leitor, item, path);

        return leitor.Falhou ? null : CondicaoDeclarada(leitor, tipo, edicaoDoExame, motivo, linhas, path);
    }

    private static IReadOnlyList<LinhaDoPredicado> LerEnquadramento(LeitorEnvelope leitor, JsonObject condicao, string path)
    {
        if (leitor.Falhou)
        {
            return [];
        }

        IReadOnlyList<LinhaDoPredicado> linhas = LeituraDasCondicoes.LerDnf(leitor, condicao, ChaveDoEnquadramento, path);
        return leitor.Falhou ? [] : linhas;
    }

    private static CondicaoDeHomologacao? CondicaoDeclarada(
        LeitorEnvelope leitor, string tipo, int edicaoDoExame, string? motivo, IReadOnlyList<LinhaDoPredicado> enquadramento, string path)
    {
        Result<PredicadoDnf?> predicado = LeituraDasCondicoes.PredicadoOpcional(enquadramento);
        Result<CondicaoDeHomologacao> condicao = CondicaoDeHomologacao.Declarar(tipo, edicaoDoExame, motivo, predicado.Value);
        FieldError[] causas = [.. condicao.Errors, .. predicado.Errors.Select(NoEnquadramento)];
        return causas.Length == 0 ? condicao.Value : leitor.Propagar<CondicaoDeHomologacao?>(causas, path);
    }

    private static FieldError NoEnquadramento(FieldError causa) =>
        (causa with { Field = causa.Field ?? string.Empty }).NoCaminhoDe(ChaveDoEnquadramento);

    private static string? TextoNuloOuNaoVazio(LeitorEnvelope leitor, JsonObject objeto, string chave, string path) =>
        !leitor.Falhou && objeto[chave] is not null ? leitor.TextoNaoVazio(objeto, chave, path) : null;

}
