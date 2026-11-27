namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A data de referência do prazo de emissão ou da validade: a fase que ela cita, pela chave, vem das fases que a leitura adotou. A fase
/// inexistente recusa o campo da chave, sem repetir o texto digitado; a que a leitura recusou deixa a data cortada, sem causa nova.
/// </summary>
internal static class LeituraDaDataDeReferenciaDoDocumento
{
    private const string CampoDaFase = "faseChave";

    internal static ParteLida<DataDeReferenciaDoDocumento> Ler(DataDeReferenciaNoDocumento? referencia, string campo, FasesLidas fases)
    {
        if (referencia is null)
        {
            return ParteLida<DataDeReferenciaDoDocumento>.Ausente;
        }

        return referencia.Tipo is { } tipo
            ? ComTipo(referencia, tipo, campo, fases)
            : ParteLida<DataDeReferenciaDoDocumento>.Recusada(TipoDaDataDeReferenciaDaEntrada.Ler(null).Errors.Select(recusa => recusa.NoCaminhoDoItem($"{campo}.tipo")));
    }

    private static ParteLida<DataDeReferenciaDoDocumento> ComTipo(
        DataDeReferenciaNoDocumento referencia, TipoDaDataDeReferencia tipo, string campo, FasesLidas fases)
    {
        ParteCitadaNoDocumento? fase = referencia.FaseChave is { } chave ? ParteCitadaNoDocumento.De(chave, fases) : null;

        return fase switch
        {
            { Inexistente: true } => ParteLida<DataDeReferenciaDoDocumento>.Recusada([FaseInexistente(campo)]),
            { Id: null } => ParteLida<DataDeReferenciaDoDocumento>.CortadaSemCausa,
            _ => Criada(DataDeReferenciaDoDocumento.Criar(tipo, referencia.Data, fase?.Id), campo),
        };
    }

    private static ParteLida<DataDeReferenciaDoDocumento> Criada(Result<DataDeReferenciaDoDocumento> criada, string campo) =>
        criada.IsSuccess
            ? ParteLida<DataDeReferenciaDoDocumento>.Montada(criada.Value!)
            : ParteLida<DataDeReferenciaDoDocumento>.Recusada(criada.Errors.Select(recusa => NoCampoDoDocumento(recusa).NoCaminhoDoItem(campo)));

    private static FieldError NoCampoDoDocumento(FieldError recusa) => recusa.Field == "faseId" ? recusa with { Field = CampoDaFase } : recusa;

    private static FieldError FaseInexistente(string campo) => new(
        $"{campo}.{CampoDaFase}",
        new DomainError(DataDeReferenciaDoDocumentoErrorCodes.FaseNaoPertenceAoProcesso, "A fase da data de referência não está no cronograma deste processo."));
}
