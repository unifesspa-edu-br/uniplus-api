namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A identificação e o cronograma que a leitura do documento entregou como declarações, e a origem que liga as fases à posição em que
/// vieram e guarda as partes que a leitura recusou. O que o documento traz de errado não impede as outras partes: a leitura acumula.
/// </summary>
public sealed record IdentificacaoECronogramaLidos(IdentidadeDoProcessoDeclarada Identidade, CronogramaDeclarado Cronograma, OrigemNoDocumento Origem)
{
    /// <summary>Lê a identificação e as fases do documento contra os cadastros citados, lidos antes num instante único.</summary>
    public static IdentificacaoECronogramaLidos Ler(DeclaracoesDoDocumento declaracoes, CadastrosCitadosNoDocumento cadastros)
    {
        ArgumentNullException.ThrowIfNull(declaracoes);
        ArgumentNullException.ThrowIfNull(cadastros);

        IdentificacaoLida identificacao = LeituraDaIdentificacao.Ler(declaracoes.Identificacao, cadastros);
        FasesLidas fases = LeituraDasFases.Ler(declaracoes.Fases, cadastros);

        return new(
            identificacao.Identidade,
            new CronogramaDeclarado(identificacao.Localidade, identificacao.AlgoritmoDeContagem, fases.Fases),
            OrigemNoDocumento.Reunir([OrigemNoDocumento.SoComPartesRecusadas(identificacao.PartesRecusadas), fases.Origem]));
    }
}
