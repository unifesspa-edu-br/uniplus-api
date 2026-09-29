namespace Unifesspa.UniPlus.Regras.Formularios;

using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// O que o avaliador recebe além da descrição do formulário: as respostas do candidato, as etapas
/// que ele já concluiu e os fatos já conhecidos de fora do formulário — os coletados por uma
/// finalidade anterior e os derivados pelo sistema, como o grupo da convocação.
/// </summary>
/// <remarks>
/// Uma resposta a fato que o formulário não produz é ignorada: a descrição é a autoridade sobre o
/// que existe, e uma resposta órfã não cria fato.
/// </remarks>
public sealed record EntradaAvaliacaoFormulario(
    IReadOnlyDictionary<string, JsonElement> Respostas,
    IReadOnlySet<string> EtapasConcluidas,
    IReadOnlyDictionary<string, FatoResolvido> FatosConhecidos);

/// <summary>O resultado da avaliação: o estado final de cada fato, de cada item e de cada termo.</summary>
public sealed record AvaliacaoFormulario(
    IReadOnlyDictionary<string, FatoResolvido> Fatos,
    IReadOnlyList<AvaliacaoItem> Itens,
    IReadOnlyList<AvaliacaoTermo> Termos);

/// <summary>
/// A avaliação de um item: se aparece, se é obrigatório e quais restrições a resposta viola. Uma
/// resposta que viola restrição não vale — o fato do item é resolvido como se não houvesse resposta.
/// </summary>
public sealed record AvaliacaoItem(
    string FatoCodigo,
    string EtapaCodigo,
    Ternario Visivel,
    Ternario Obrigatorio,
    IReadOnlyList<RestricaoValor> RestricoesVioladas);

/// <summary>A avaliação de um termo: se aparece e se é obrigatório.</summary>
public sealed record AvaliacaoTermo(string Codigo, Ternario Visivel, Ternario Obrigatorio);
