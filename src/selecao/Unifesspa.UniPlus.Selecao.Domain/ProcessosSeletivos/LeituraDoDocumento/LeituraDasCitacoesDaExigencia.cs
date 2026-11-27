namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A fase, a etapa e o tipo de documento que a exigência cita, e as recusas das citações que não se resolvem.</summary>
internal sealed record CitacoesDaExigencia(Guid? FaseId, Guid? EtapaId, bool CitaEtapa, CopiaDoTipoDeDocumento? Tipo, IReadOnlyList<FieldError> Recusas)
{
    /// <summary>Se todas as citações se resolveram: a que a leitura recusou deixa a exigência sem citação e sem causa nova.</summary>
    internal bool Completas => FaseId is not null && Tipo is not null && (!CitaEtapa || EtapaId is not null);
}

/// <summary>
/// Resolve as três citações da exigência: a fase e a etapa pela chave, entre as que a leitura adotou ou recusou, e o tipo de documento
/// pelo código, no cadastro. A fase é obrigatória e a etapa é opcional. A parte inexistente é recusa da exigência, no campo que a cita, sem
/// repetir o texto digitado; a parte que a leitura recusou deixa a citação sem causa nova, porque a causa é a da parte.
/// </summary>
internal static class LeituraDasCitacoesDaExigencia
{
    private const string CampoDaFase = "faseId";
    private const string CampoDaEtapa = "etapaId";
    private const string CampoDoTipo = "tipoDocumentoId";
    private const string CodigoDaFase = "DocumentoExigido.FaseNaoPertenceAoProcesso";
    private const string CodigoDaEtapa = "DocumentoExigido.EtapaNaoPertenceAoProcesso";

    internal static CitacoesDaExigencia Ler(ExigenciaNoDocumento exigencia, CadastrosCitadosNoDocumento cadastros, FasesLidas fases, EtapasLidas etapas)
    {
        (Guid? faseId, FieldError? faltaDaFase) = Fase(exigencia.FaseChave, fases);
        (Guid? etapaId, FieldError? faltaDaEtapa) = Etapa(exigencia.EtapaChave, etapas);
        CopiaDoTipoDeDocumento? tipo = cadastros.TipoDeDocumento(exigencia.TipoDocumentoCodigo);

        return new(faseId, etapaId, exigencia.EtapaChave is not null, tipo, [.. new[] { faltaDaFase, faltaDaEtapa, FaltaDoTipo(tipo) }.OfType<FieldError>()]);
    }

    private static (Guid? Id, FieldError? Recusa) Fase(string? chave, FasesLidas fases) =>
        chave is null
            ? (null, new(CampoDaFase, new DomainError(CodigoDaFase, "A exigência deve indicar a fase do cronograma em que o documento é exigido.")))
            : Resolvida(ParteCitadaNoDocumento.De(chave, fases), CampoDaFase, new(CodigoDaFase, "A fase da exigência não está no cronograma deste processo."));

    private static (Guid? Id, FieldError? Recusa) Etapa(string? chave, EtapasLidas etapas) =>
        chave is null
            ? (null, null)
            : Resolvida(ParteCitadaNoDocumento.De(chave, etapas), CampoDaEtapa, new(CodigoDaEtapa, "A etapa da exigência não é uma etapa deste processo."));

    private static (Guid? Id, FieldError? Recusa) Resolvida(ParteCitadaNoDocumento parte, string campo, DomainError recusa) =>
        parte.Inexistente ? (null, new(campo, recusa)) : (parte.Id, null);

    private static FieldError? FaltaDoTipo(CopiaDoTipoDeDocumento? tipo) =>
        tipo is null
            ? new(CampoDoTipo, new DomainError(ProcessoSeletivoErrorCodes.TipoDocumentoDoVinculoNaoEncontrado, "O tipo de documento não foi encontrado ou não está mais disponível."))
            : null;
}
