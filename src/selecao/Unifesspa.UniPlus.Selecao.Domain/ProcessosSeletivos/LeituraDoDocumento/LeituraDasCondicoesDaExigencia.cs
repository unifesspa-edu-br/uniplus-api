namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// As condições que tornam a exigência aplicável: cada alternativa é uma cláusula, e as condições dela valem juntas. Confere só a forma
/// de cada condição, com a fábrica de domínio, e acumula as recusas de todas, cada uma na posição da alternativa e da condição. O dado
/// que a condição cita ser do vocabulário do processo é conferido antes de congelar, e não aqui.
/// </summary>
internal static class LeituraDasCondicoesDaExigencia
{
    private const string Campo = "condicoes";
    private const string CampoDoDado = "codigoDoDado";

    private static readonly JsonElement SemValor = JsonSerializer.SerializeToElement<string?>(null);

    internal static ParteLida<IReadOnlyList<CondicaoGatilho>> Ler(IReadOnlyList<IReadOnlyList<CondicaoSobreDadoNoDocumento>> alternativas)
    {
        (CondicaoGatilho? Condicao, FieldError[] Recusas)[] lidas =
        [
            .. alternativas.SelectMany((alternativa, clausula) => alternativa.Select((condicao, posicao) => Condicao(clausula, posicao, condicao))),
        ];
        FieldError[] recusas = [.. lidas.SelectMany(static lida => lida.Recusas)];

        return recusas.Length > 0
            ? ParteLida<IReadOnlyList<CondicaoGatilho>>.Recusada(recusas)
            : ParteLida<IReadOnlyList<CondicaoGatilho>>.Montada([.. lidas.Select(static lida => lida.Condicao!)]);
    }

    private static (CondicaoGatilho? Condicao, FieldError[] Recusas) Condicao(int clausula, int posicao, CondicaoSobreDadoNoDocumento citada)
    {
        Result<CondicaoDnf> lida = CondicaoDnf.CriarInformada(citada.CodigoDoDado, citada.Operador, citada.Valor ?? SemValor);
        FieldError[] recusas = [.. RecusasDaCondicao(lida, citada.Valor is null).Select(recusa => NoCampoDoDocumento(recusa).NoCaminhoDe($"{Campo}[{clausula}][{posicao}]"))];

        return recusas.Length > 0 ? (null, recusas) : (CondicaoGatilho.Criar(clausula, lida.Value!.Fato, lida.Value.Operador, lida.Value.Valor).Value, []);
    }

    // O valor ausente é recusa do valor, com o operador ou sem ele; a recusa da forma do valor que a fábrica devolveria para o nulo não se repete.
    private static IEnumerable<FieldError> RecusasDaCondicao(Result<CondicaoDnf> lida, bool semValor) =>
        semValor
            ? [.. lida.Errors.Where(static recusa => recusa.Field != CondicaoDnf.CampoValor), new(CondicaoDnf.CampoValor, new DomainError("CondicaoDnf.FormaIncoerenteComOperador", "O valor da condição é obrigatório."))]
            : lida.Errors;

    private static FieldError NoCampoDoDocumento(FieldError recusa) =>
        recusa.Field == CondicaoDnf.CampoFato ? recusa with { Field = CampoDoDado } : recusa;
}
