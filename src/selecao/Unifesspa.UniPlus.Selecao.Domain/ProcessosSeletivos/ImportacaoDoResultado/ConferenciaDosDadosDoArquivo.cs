namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ImportacaoDoResultado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;

/// <summary>
/// O dado que um relatório do processo declara no arquivo, em coluna obrigatória ou opcional, e o dado básico de que ele declara algum fato têm a importação como produtora, e nenhum campo de
/// formulário o coleta (UNI-REQ-0144). A única exceção é o dado básico (UNI-REQ-0151) que algum relatório
/// considerado não traz inteiro: o formulário obrigatório da primeira fase depois da importação o coleta de
/// quem não o recebeu; em outro formulário ou em outra fase, é recusado. Sem a primeira fase declarada, a
/// exceção não é avaliada, e a recusa que orienta é a da primeira fase por declarar. O fato que nenhum
/// relatório declara não conflita com campo algum. A recusa aponta o fato pelo caminho.
/// </summary>
public static class ConferenciaDosDadosDoArquivo
{
    public static IReadOnlyList<FieldError> Conferir(
        IReadOnlyList<ColetaDoFormulario> coletas,
        RelatoriosConsiderados relatorios,
        ProcessoDosFormularios processo,
        CatalogoDosFatosCitados catalogo)
    {
        ArgumentNullException.ThrowIfNull(coletas);
        ArgumentNullException.ThrowIfNull(relatorios);
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentNullException.ThrowIfNull(catalogo);

        IReadOnlySet<string> aComplementar = relatorios.DadosBasicosAComplementar(ConjuntoBasicoDaInscricao.Fatos, catalogo.PresencasNaImportacao);
        return [.. coletas
            .SelectMany(coleta => coleta.FatosDosCampos.Where(fato => VemNoArquivo(fato, relatorios, catalogo)).Select(fato => (fato, coleta)))
            .Select(campo => (campo.fato, codigo: CodigoDaRecusa(campo.coleta, campo.fato, aComplementar, processo)))
            .Where(static recusa => recusa.codigo is not null)
            .DistinctBy(static recusa => (recusa.fato, recusa.codigo))
            .Select(static recusa => new FieldError($"fatos.{recusa.fato}", new DomainError(recusa.codigo!, MensagemDa(recusa.codigo!))))];
    }

    /// <summary>O fato vem no arquivo quando algum relatório o declara ou declara outro fato do mesmo dado básico, que vale inteiro.</summary>
    private static bool VemNoArquivo(string fato, RelatoriosConsiderados relatorios, CatalogoDosFatosCitados catalogo) =>
        (catalogo.PresencasNaImportacao.FirstOrDefault(presenca => presenca.FatosDoDadoBasico.Contains(fato))?.FatosDoDadoBasico ?? new HashSet<string> { fato })
            .Any(relatorios.Declara);

    private static string? CodigoDaRecusa(ColetaDoFormulario coleta, string fato, IReadOnlySet<string> aComplementar, ProcessoDosFormularios processo)
    {
        if (!aComplementar.Contains(fato))
        {
            return ImportacaoDoResultadoErrorCodes.FatoColetadoPorFormulario;
        }

        return processo.PrimeiraFaseDepoisDaImportacaoId is null || EhOObrigatorioDaPrimeiraFase(coleta, processo)
            ? null
            : ImportacaoDoResultadoErrorCodes.DadoBasicoComplementarForaDaPrimeiraFase;
    }

    private static bool EhOObrigatorioDaPrimeiraFase(ColetaDoFormulario coleta, ProcessoDosFormularios processo) =>
        coleta.Formulario.Obrigatorio && coleta.Formulario.FaseId == processo.PrimeiraFaseDepoisDaImportacaoId;

    private static string MensagemDa(string codigo) =>
        codigo == ImportacaoDoResultadoErrorCodes.FatoColetadoPorFormulario
            ? "O fato vem no arquivo da importação e não é coletado por campo de formulário."
            : "O dado básico que o arquivo não traz inteiro só é coletado pelo formulário obrigatório da primeira fase depois da importação.";
}
