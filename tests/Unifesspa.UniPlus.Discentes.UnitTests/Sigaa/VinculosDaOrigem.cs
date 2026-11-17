namespace Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Acl;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Contracts;

/// <summary>
/// Vínculos como a origem os entrega, e a decodificação deles, para os testes que partem
/// de um vínculo completo e alteram só o que querem provar.
/// </summary>
internal static class VinculosDaOrigem
{
    /// <summary>
    /// Vínculo com todos os campos, nos moldes do exemplo que o contrato da origem publica.
    /// </summary>
    public static VinculoDiscentePayload Completo() => new()
    {
        IdDiscente = 24786,
        Matricula = "201446010001",
        Cpf = "52998224725",
        Nome = "FULANO DE TAL",
        Nivel = "G",
        Curso = CursoCompleto(),
        Situacao = new SituacaoPayload { Id = 1, Descricao = "ATIVO", SituacaoVinculo = "ATV" },
        AnoIngresso = 2020,
        PeriodoIngresso = 1,
        DateRequest = "2026-07-15 13:09:52",
    };

    private static CursoPayload CursoCompleto() => new()
    {
        Id = 42,
        Nome = "CIÊNCIA DA COMPUTAÇÃO",
        CodigoEmec = "1269997",
        Unidade = new UnidadePayload { Id = 12, Nome = "INSTITUTO DE CIENCIAS EXATAS" },
    };

    /// <summary>
    /// O vínculo completo, com o curso trocado pelo mesmo curso sem unidade acadêmica.
    /// </summary>
    public static VinculoDiscentePayload SemUnidadeAcademica() => ComCurso(curso => curso with { Unidade = null });

    /// <summary>
    /// O vínculo completo, com o curso trocado pelo que <paramref name="alterar"/> devolve.
    /// </summary>
    public static VinculoDiscentePayload ComCurso(Func<CursoPayload, CursoPayload> alterar) =>
        Completo() with { Curso = alterar(Completo().Curso!) };

    /// <summary>
    /// O vínculo completo, com a situação trocada pela que <paramref name="alterar"/> devolve.
    /// </summary>
    public static VinculoDiscentePayload ComSituacao(Func<SituacaoPayload, SituacaoPayload> alterar) =>
        Completo() with { Situacao = alterar(Completo().Situacao!) };

    public static string Texto(int tamanho) => new('A', tamanho);

    public static ResultadoDaDecodificacao Decodificar(params VinculoDiscentePayload[] pagina) =>
        DecodificadorDeVinculos.Decodificar(pagina);

    public static VinculoDecodificado DecodificarUm(VinculoDiscentePayload unico) =>
        Decodificar(unico).Aceitos[0];
}
