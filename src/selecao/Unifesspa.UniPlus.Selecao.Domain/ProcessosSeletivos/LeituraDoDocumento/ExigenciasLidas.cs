namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As exigências de documentos e a árvore de comprovação que a leitura do documento entregou como declaração, e a origem que liga cada
/// item adotado à posição em que veio e guarda as partes que a leitura recusou. O bloco ausente é o processo sem comprovação documental declarada.
/// O que o documento traz de errado não impede as outras partes: a leitura acumula.
/// </summary>
public sealed record ExigenciasLidas(ExigenciasDeclaradas Exigencias, OrigemNoDocumento Origem)
{
    /// <summary>Lê o bloco contra os cadastros citados, lidos antes num instante único, e as fases e etapas que a leitura adotou ou recusou.</summary>
    internal static ExigenciasLidas Ler(
        DeclaracoesDoDocumento declaracoes, CadastrosCitadosNoDocumento cadastros, FasesLidas fases, EtapasLidas etapas)
    {
        ArgumentNullException.ThrowIfNull(declaracoes);
        ArgumentNullException.ThrowIfNull(cadastros);
        ArgumentNullException.ThrowIfNull(fases);
        ArgumentNullException.ThrowIfNull(etapas);

        return declaracoes.Exigencias is { } bloco
            ? Ler(bloco, cadastros, fases, etapas)
            : new(new([], []), OrigemNoDocumento.Identidade);
    }

    private static ExigenciasLidas Ler(ExigenciasNoDocumento bloco, CadastrosCitadosNoDocumento cadastros, FasesLidas fases, EtapasLidas etapas)
    {
        DocumentosLidos documentos = LeituraDosDocumentosExigidos.Ler(bloco.Exigencias, cadastros, fases, etapas);
        ArvoreLida arvore = LeituraDaArvoreDeComprovacao.Ler(bloco.RegrasDeComprovacao, documentos);

        return new(
            new([.. documentos.Adotados.Select(static adotado => adotado.Documento)], arvore.Regras),
            OrigemNoDocumento.Reunir([OrigemNoDocumento.SoComPartesRecusadas([.. documentos.Recusadas, .. arvore.Recusadas]), .. ListasDasExigencias.Origens(documentos, arvore.Raizes)]));
    }
}
