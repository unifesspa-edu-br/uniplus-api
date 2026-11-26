namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

public sealed class DescricaoDoDocumentoDoRascunho(
    IEsquemaDoDocumentoDaConfiguracao esquema, IEnumerable<IDescricaoDeCadastro> descricoes)
{
    public async Task DescreverAsync(JsonObject documento, CancellationToken cancellationToken)
    {
        IReadOnlyList<NoDoDocumento> nos = NosDeclaradosNoEsquema.Listar(documento, esquema.Forma);
        List<Citacao> citacoes = Coletar(nos);
        List<CitacaoEmLista> listas = ColetarListas(nos);
        foreach (IDescricaoDeCadastro descricao in descricoes)
        {
            List<Citacao> citadas = citacoes.FindAll(citacao => citacao.Campo.Cadastro == descricao.Cadastro);
            List<CitacaoEmLista> listadas = listas.FindAll(citacao => citacao.Campo.Cadastro == descricao.Cadastro);
            if (citadas.Count + listadas.Count > 0)
            {
                await DescreverCitadasAsync(descricao, citadas, listadas, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task DescreverCitadasAsync(
        IDescricaoDeCadastro descricao, List<Citacao> citadas, List<CitacaoEmLista> listadas, CancellationToken cancellationToken)
    {
        (string Codigo, string? Complemento)[] codigos = [.. citadas.Select(static citacao => citacao.Codigo)
            .Concat(listadas.SelectMany(static citacao => citacao.Codigos).Select(static codigo => (codigo, (string?)null))).Distinct()];
        IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>> atributos = await descricao.LerAsync(codigos, cancellationToken).ConfigureAwait(false);
        citadas.ForEach(citacao => EscreverAtributos(citacao, atributos));
        listadas.ForEach(citacao => EscreverNomes(citacao, atributos));
    }

    private static List<Citacao> Coletar(IReadOnlyList<NoDoDocumento> nos) =>
        [.. from no in nos
            where no.Valor is JsonObject
            from campo in CamposDeContextoDoDocumento.Todos
            where CamposDeContextoDoDocumento.Corresponde(campo.Caminho, no.Ponteiro)
            let objeto = (JsonObject)no.Valor
            where objeto[campo.Codigo] is JsonValue valor && valor.TryGetValue<string>(out _)
            where campo.CodigoComplementar is null || objeto[campo.CodigoComplementar] is JsonValue complemento && complemento.TryGetValue<string>(out _)
            select new Citacao(objeto, no.Forma, campo, (objeto[campo.Codigo]!.GetValue<string>(),
                campo.CodigoComplementar is null ? null : objeto[campo.CodigoComplementar]!.GetValue<string>()))];

    private static List<CitacaoEmLista> ColetarListas(IReadOnlyList<NoDoDocumento> nos) =>
        [.. from no in nos
            where no.Valor is JsonObject
            from campo in CamposDeContextoDoDocumento.ListasDeCodigos
            where CamposDeContextoDoDocumento.Corresponde(campo.Caminho, no.Ponteiro)
            let objeto = (JsonObject)no.Valor
            let codigos = TextosDaLista(objeto[campo.Codigos])
            where codigos.Count > 0
            select new CitacaoEmLista(objeto, no.Forma, campo, codigos)];

    private static List<string> TextosDaLista(JsonNode? lista) =>
        lista is JsonArray itens && itens.All(static item => item is JsonValue valor && valor.TryGetValue<string>(out _))
            ? [.. itens.Select(static item => item!.GetValue<string>())]
            : [];

    private static void EscreverAtributos(Citacao citacao, IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>> atributos)
    {
        if (atributos.TryGetValue(citacao.Codigo, out IReadOnlyList<object?>? valores))
        {
            Escrever(citacao, valores);
        }
        else if (citacao.Campo.Cadastro == CamposDeContextoDoDocumento.MotivosDeDecisao)
        {
            Escrever(citacao, [null, null, null, null, 1]);
        }
    }

    private static void Escrever(Citacao citacao, IReadOnlyList<object?> valores)
    {
        int posicao = citacao.Objeto.IndexOf(citacao.Campo.Codigo) + 1;
        for (int indice = 0; indice < citacao.Campo.Atributos.Count; indice++)
        {
            string nome = citacao.Campo.Atributos[indice];
            if (valores[indice] is not null && citacao.Forma.DeclaraSomenteLeitura(nome))
            {
                Inserir(citacao.Objeto, nome, posicao++, valores[indice]!);
            }
        }
    }

    // A lista de nomes só sai inteira: o nome que falta tiraria a correspondência de posição com a lista de códigos.
    private static void EscreverNomes(CitacaoEmLista citacao, IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>> atributos)
    {
        List<string> nomes = [.. citacao.Codigos
            .Select(codigo => atributos.TryGetValue((codigo, null), out IReadOnlyList<object?>? valores) ? valores[0] as string : null)
            .OfType<string>()];
        if (nomes.Count == citacao.Codigos.Count && citacao.Forma.DeclaraSomenteLeitura(citacao.Campo.Nomes))
        {
            Inserir(citacao.Objeto, citacao.Campo.Nomes, citacao.Objeto.IndexOf(citacao.Campo.Codigos) + 1, nomes);
        }
    }

    private static void Inserir(JsonObject objeto, string nome, int posicao, object valor)
    {
        objeto.Remove(nome);
        objeto.Insert(posicao, nome, ValorJson(valor));
    }

    private static JsonNode? ValorJson(object valor) => valor switch
    {
        string texto => JsonValue.Create(texto),
        int numero => JsonValue.Create(numero),
        IReadOnlyList<string> textos => ListaDeTextos(textos),
        JsonNode no => no.DeepClone(),
        _ => throw new InvalidOperationException("Tipo de atributo inesperado."),
    };

    private static JsonArray ListaDeTextos(IReadOnlyList<string> textos) =>
        new([.. textos.Select(static texto => (JsonNode?)JsonValue.Create(texto))]);

    private sealed record Citacao(JsonObject Objeto, FormaDoNo Forma, CampoDeContexto Campo, (string Codigo, string? Complemento) Codigo);

    private sealed record CitacaoEmLista(JsonObject Objeto, FormaDoNo Forma, ListaDeCodigosDeContexto Campo, IReadOnlyList<string> Codigos);
}
