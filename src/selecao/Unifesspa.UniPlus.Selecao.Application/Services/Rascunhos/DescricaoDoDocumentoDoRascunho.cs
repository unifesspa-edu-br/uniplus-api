namespace Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Application.Services.Rascunhos.Descricoes;

public sealed class DescricaoDoDocumentoDoRascunho(
    IEsquemaDoDocumentoDaConfiguracao esquema, IEnumerable<IDescricaoDeCadastro> descricoes)
{
    public async Task DescreverAsync(JsonObject documento, CancellationToken cancellationToken)
    {
        List<Citacao> citacoes = Coletar(documento);
        foreach (IDescricaoDeCadastro descricao in descricoes)
        {
            List<Citacao> citadas = citacoes.FindAll(citacao => citacao.Campo.Cadastro == descricao.Cadastro);
            if (citadas.Count > 0)
            {
                await DescreverCitadasAsync(descricao, citadas, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task DescreverCitadasAsync(
        IDescricaoDeCadastro descricao, List<Citacao> citadas, CancellationToken cancellationToken)
    {
        (string Codigo, string? Complemento)[] codigos = [.. citadas.Select(static citacao => citacao.Codigo).Distinct()];
        IReadOnlyDictionary<(string Codigo, string? Complemento), IReadOnlyList<object?>> atributos = await descricao.LerAsync(codigos, cancellationToken).ConfigureAwait(false);
        foreach (Citacao citacao in citadas)
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
    }

    private List<Citacao> Coletar(JsonObject documento) =>
        [.. from no in NosDeclaradosNoEsquema.Listar(documento, esquema.Forma)
            where no.Valor is JsonObject
            from campo in CamposDeContextoDoDocumento.Todos
            where CamposDeContextoDoDocumento.Corresponde(campo.Caminho, no.Ponteiro)
            let objeto = (JsonObject)no.Valor
            where objeto[campo.Codigo] is JsonValue valor && valor.TryGetValue<string>(out _)
            where campo.CodigoComplementar is null || objeto[campo.CodigoComplementar] is JsonValue complemento && complemento.TryGetValue<string>(out _)
            select new Citacao(objeto, no.Forma, campo, (objeto[campo.Codigo]!.GetValue<string>(),
                campo.CodigoComplementar is null ? null : objeto[campo.CodigoComplementar]!.GetValue<string>()))];

    private static void Escrever(Citacao citacao, IReadOnlyList<object?> valores)
    {
        int posicao = citacao.Objeto.IndexOf(citacao.Campo.Codigo) + 1;
        for (int indice = 0; indice < citacao.Campo.Atributos.Count; indice++)
        {
            string nome = citacao.Campo.Atributos[indice];
            if (valores[indice] is not null && citacao.Forma.Propriedades.TryGetValue(nome, out FormaDoNo? forma) && forma.SomenteLeitura)
            {
                citacao.Objeto.Remove(nome);
                citacao.Objeto.Insert(posicao++, nome, ValorJson(valores[indice]!));
            }
        }
    }

    private static JsonValue? ValorJson(object valor) => valor switch
    {
        string texto => JsonValue.Create(texto),
        int numero => JsonValue.Create(numero),
        _ => throw new InvalidOperationException("Tipo de atributo inesperado."),
    };

    private sealed record Citacao(JsonObject Objeto, FormaDoNo Forma, CampoDeContexto Campo, (string Codigo, string? Complemento) Codigo);
}
