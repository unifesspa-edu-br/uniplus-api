namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O prazo de emissão e a validade do documento, cada um criado pela fábrica de domínio. O bloco ausente é nulo; o presente sem escolha
/// reconhecida é recusa da escolha, junto com as recusas da data de referência e dos detalhes, que a fábrica acumula.
/// </summary>
internal static class LeituraDoPrazoEDaValidadeDaExigencia
{
    private const string CampoDoPrazo = "prazoDeEmissao";
    private const string CampoDaValidade = "validade";

    internal static ParteLida<PrazoDeEmissao> Prazo(PrazoDeEmissaoNoDocumento? prazo, FasesLidas fases)
    {
        if (prazo is null)
        {
            return ParteLida<PrazoDeEmissao>.Ausente;
        }

        ParteLida<DataDeReferenciaDoDocumento> referencia = LeituraDaDataDeReferenciaDoDocumento.Ler(prazo.Referencia, $"{CampoDoPrazo}.referencia", fases);
        if (prazo.Escolha is not { } escolha)
        {
            return Recusada<PrazoDeEmissao>(referencia, Escolha(CampoDoPrazo, PrazoDeEmissaoErrorCodes.EscolhaInvalida, "A escolha do prazo de emissão não é reconhecida."));
        }

        return PodeCriar(referencia)
            ? ParteLida<PrazoDeEmissao>.Do(PrazoDeEmissao.Criar(escolha, prazo.Valor, prazo.Unidade, referencia.Valor, prazo.EfeitoForaDoPrazo), CampoDoPrazo)
            : Recusada<PrazoDeEmissao>(referencia, DetalhesDoPrazo(escolha, prazo));
    }

    internal static ParteLida<ValidadeDoDocumento> Validade(ValidadeNoDocumento? validade, FasesLidas fases)
    {
        if (validade is null)
        {
            return ParteLida<ValidadeDoDocumento>.Ausente;
        }

        ParteLida<DataDeReferenciaDoDocumento> referencia = LeituraDaDataDeReferenciaDoDocumento.Ler(validade.Referencia, $"{CampoDaValidade}.referencia", fases);
        if (validade.Escolha is not { } escolha)
        {
            return Recusada<ValidadeDoDocumento>(referencia, Escolha(CampoDaValidade, ValidadeDoDocumentoErrorCodes.EscolhaInvalida, "A escolha da validade não é reconhecida."));
        }

        return PodeCriar(referencia)
            ? ParteLida<ValidadeDoDocumento>.Do(ValidadeDoDocumento.Criar(escolha, referencia.Valor, validade.EfeitoDoVencimento), CampoDaValidade)
            : Recusada<ValidadeDoDocumento>(referencia, DetalhesDaValidade(escolha, validade));
    }

    private static bool PodeCriar(ParteLida<DataDeReferenciaDoDocumento> referencia) => referencia.Recusas.Count == 0 && !referencia.Cortada;

    // A referência recusada ou cortada impede a fábrica, mas a recusa dos campos que não dependem dela continua valendo.
    private static ParteLida<T> Recusada<T>(ParteLida<DataDeReferenciaDoDocumento> referencia, IEnumerable<FieldError> independentes)
        where T : class =>
        new(null, [.. referencia.Recusas, .. independentes], referencia.Cortada);

    private static IEnumerable<FieldError> DetalhesDoPrazo(EscolhaDoPrazoDeEmissao escolha, PrazoDeEmissaoNoDocumento prazo) =>
        escolha == EscolhaDoPrazoDeEmissao.ComPrazo
            ? PrazoDeEmissao.ValidarDetalhesIndependentesDaReferencia(prazo.Valor, prazo.Unidade, prazo.EfeitoForaDoPrazo).Select(static recusa => recusa.NoCaminhoDoItem(CampoDoPrazo))
            : [];

    private static IEnumerable<FieldError> DetalhesDaValidade(EscolhaDaValidade escolha, ValidadeNoDocumento validade) =>
        escolha == EscolhaDaValidade.ExigeDocumentoValido
            ? ValidadeDoDocumento.ValidarDetalhesIndependentesDaReferencia(validade.EfeitoDoVencimento).Select(static recusa => recusa.NoCaminhoDoItem(CampoDaValidade))
            : [];

    private static IEnumerable<FieldError> Escolha(string campo, string codigo, string mensagem) =>
        [new($"{campo}.escolha", new DomainError(codigo, mensagem))];
}
