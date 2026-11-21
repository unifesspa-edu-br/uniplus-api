namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As cópias do tipo de banca que as bancas dos testes guardam. O papel e os tipos de decisão julgados seguem o que o
/// vocabulário prevê para cada tipo; <see cref="Com"/> troca o papel quando o teste precisa de outro.
/// </summary>
internal static class TiposDeBancaCopiadosDeTeste
{
    /// <summary>A cópia do tipo com o papel e os tipos de decisão julgados que o vocabulário prevê para ele.</summary>
    public static TipoDeBancaCopiado De(TipoDeBanca codigo, Guid? origemId = null) =>
        Com(codigo, PapelNatural(codigo), origemId);

    /// <summary>A cópia do tipo de banca que julga recurso em 1ª instância.</summary>
    public static TipoDeBancaCopiado DeRecursos(Guid? origemId = null) => De(TipoDeBanca.AnaliseDeRecursos, origemId);

    /// <summary>A cópia do tipo de banca que avalia o mérito, com o nome que o teste escolher.</summary>
    public static TipoDeBancaCopiado Avaliadora(Guid? origemId = null, string? nome = null) =>
        Nomeada(De(TipoDeBanca.CorrecaoDeRedacoes, origemId), nome);

    /// <summary>A cópia do tipo com outro papel; só quem avalia o mérito declara tipos de decisão julgados.</summary>
    public static TipoDeBancaCopiado Com(
        TipoDeBanca codigo,
        PapelDaBanca papel,
        Guid? origemId = null,
        string? nome = null,
        bool exigeDuasOuMaisBancas = false,
        bool encerraRecursoNaPrimeiraInstancia = false,
        IEnumerable<TipoMotivoDeDecisao>? tiposDeDecisaoJulgados = null) =>
        new(
            origemId ?? Guid.CreateVersion7(),
            codigo,
            nome ?? codigo.Rotulo(),
            papel,
            exigeDuasOuMaisBancas,
            encerraRecursoNaPrimeiraInstancia,
            tiposDeDecisaoJulgados ?? (papel == PapelDaBanca.AvaliaOMerito ? [TipoDeDecisaoNatural(codigo)] : []));

    /// <summary>A mesma cópia, com o nome do cadastro trocado.</summary>
    public static TipoDeBancaCopiado Nomeada(TipoDeBancaCopiado tipo, string? nome) =>
        nome is null
            ? tipo
            : new(
                tipo.OrigemId,
                tipo.Codigo,
                nome,
                tipo.Papel,
                tipo.ExigeDuasOuMaisBancas,
                tipo.EncerraRecursoNaPrimeiraInstancia,
                tipo.TiposDeDecisaoJulgados);

    private static PapelDaBanca PapelNatural(TipoDeBanca codigo) => codigo switch
    {
        TipoDeBanca.AnaliseDeRecursos => PapelDaBanca.JulgaAPrimeiraInstancia,
        TipoDeBanca.ImportacaoDasNotasDoInep => PapelDaBanca.ImportaAsNotasDoInep,
        TipoDeBanca.ImportacaoDeCandidatos => PapelDaBanca.ImportaCandidatos,
        _ => PapelDaBanca.AvaliaOMerito,
    };

    private static TipoMotivoDeDecisao TipoDeDecisaoNatural(TipoDeBanca codigo) => codigo switch
    {
        TipoDeBanca.AnaliseDocumental => TipoMotivoDeDecisao.AnaliseDocumental,
        TipoDeBanca.Heteroidentificacao => TipoMotivoDeDecisao.Heteroidentificacao,
        TipoDeBanca.AvaliacaoBiopsicossocial => TipoMotivoDeDecisao.AvaliacaoBiopsicossocial,
        TipoDeBanca.HomologacaoDasInscricoes => TipoMotivoDeDecisao.HomologacaoDasInscricoes,
        _ => TipoMotivoDeDecisao.NotaDaEtapa,
    };
}
