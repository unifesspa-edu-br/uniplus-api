namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// Os limites das <b>colunas</b> que vão receber a configuração reidratada.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por que o decoder precisa deles.</b> Um valor que não cabe na coluna atravessa a
/// leitura inteira, satisfaz o domínio e <b>recanonicaliza nos mesmos bytes</b> — o
/// encoder o reemite tal qual —, de modo que a prova de round-trip <b>aprova</b>. A
/// recusa só chega no <c>SaveChanges</c>, como <c>DbUpdateException</c> do Postgres
/// (<c>22001</c> <i>value too long</i>, <c>22003</c> <i>numeric field overflow</i>) — no
/// meio do descarte, como <b>500 não tratado</b> em vez de recusa nomeada. O caminho de
/// comando já barra isso no <c>FluentValidation</c>; o de reidratação não barrava.
/// </para>
/// <para>
/// Estes valores <b>espelham o schema</b>, e não podem divergir dele: o fitness
/// <c>LimitesDoEnvelopeBatemComOSchemaTests</c> lê o modelo do EF Core e compara coluna a
/// coluna. Mudar um <c>HasMaxLength</c> ou um <c>HasPrecision</c> sem mudar aqui
/// <b>quebra o build</b> — que é o único jeito de esta lista não envelhecer em silêncio.
/// </para>
/// <para>
/// Os campos que vivem dentro de um <c>json</c>/<c>jsonb</c> (os <c>args</c> dos critérios
/// e das regras de eliminação, os <c>criteriosCumulativos</c>) <b>não</b> têm limite de
/// coluna — e por isso não aparecem aqui.
/// </para>
/// </remarks>
public static class LimitesDoEnvelope
{
    // Texto — comprimento máximo (varchar).
    public const int EtapaNome = 300;
    public const int ModalidadeCodigo = 60;
    public const int ModalidadeDescricao = 300;
    public const int Token = 30;
    public const int BaseLegal = 500;
    public const int CondicaoCodigo = 50;
    public const int NomeDeCadastro = 300;
    public const int MunicipioConvenio = 200;
    public const int CensoReferencia = 20;
    public const int RegraCodigo = 128;
    public const int RegraVersao = 16;

    /// <summary>
    /// O número do ato. Não é coluna do agregado (os <c>DadosEdital</c> são do ato, não da
    /// configuração), mas os validators de publicar e de retificar o limitam a 60 — e o
    /// decoder tem de ser tão estrito quanto eles.
    /// </summary>
    public const int NumeroDoAto = 60;

    // Story #851 — cronograma de fases.
    /// <summary><c>FaseCronograma.Codigo</c> — snapshot-copy do código canônico de <c>FaseCanonica</c>.</summary>
    public const int FaseCodigo = 60;

    /// <summary><c>FaseCronograma.DonoInstitucional</c> — snapshot-copy do dono típico.</summary>
    public const int DonoInstitucional = 60;

    /// <summary>
    /// Código de tipo de ato de Publicações — <c>FaseCronograma.AtoProduzidoCodigo</c> e
    /// <c>ArgsRegraPrazoRecurso.AtoAncoraCodigo</c> compartilham o mesmo limite.
    /// </summary>
    public const int TipoAtoCodigo = 60;

    /// <summary><c>BancaRequerida.Codigo</c> — snapshot-copy do código classificatório de <c>TipoBanca</c>.</summary>
    public const int TipoBancaCodigo = 60;

    // Story #554 (PR #903) — exigencias[] real.
    /// <summary><c>DocumentoExigido.TipoDocumentoCodigo</c> — snapshot-copy do código classificatório de <c>TipoDocumento</c>.</summary>
    public const int TipoDocumentoCodigo = 60;

    /// <summary><c>DocumentoExigido.TipoDocumentoNome</c> — snapshot-copy do rótulo legível de <c>TipoDocumento</c>.</summary>
    public const int TipoDocumentoNome = 200;

    /// <summary><c>DocumentoExigido.TipoDocumentoCategoria</c> — snapshot-copy da categoria classificatória de <c>TipoDocumento</c>.</summary>
    public const int TipoDocumentoCategoria = 60;

    /// <summary><c>DocumentoExigidoBaseLegal.Observacao</c>.</summary>
    public const int ObservacaoBaseLegal = 1000;

    /// <summary><c>CondicaoGatilho.Fato</c> — nome do fato do gatilho DNF (PR #896, Story #847/#554).</summary>
    public const int Fato = 60;

    // Decimais — precisão total (o `p` de numeric(p,s)).
    public const int PrecisaoEtapa = 18;
    public const int PrecisaoBonus = 6;
    public const int PrecisaoPr = 5;
    public const int PrecisaoPercentual = 5;

    /// <summary>Precisão de <c>ArgsRegraPrazoRecurso.PrazoValor</c> e dos dois pares de suspensividade — <c>numeric(18,4)</c>.</summary>
    public const int PrecisaoPrazo = 18;
}
