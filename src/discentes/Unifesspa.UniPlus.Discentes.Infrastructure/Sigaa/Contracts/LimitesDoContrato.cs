namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Contracts;

/// <summary>
/// Quanto texto a origem promete entregar em cada campo do vínculo.
/// </summary>
/// <remarks>
/// São os <c>maxLength</c> declarados no schema do contrato, e são menores do que as
/// colunas da réplica comportam. A folga entre os dois não é permissão: texto acima do
/// limite declarado é entrega fora do combinado, mesmo cabendo na coluna, e precisa ser
/// contabilizado como tal — do contrário a origem passaria a divergir do que promete sem
/// que nada apontasse a divergência.
///
/// Campo que o contrato deixa sem limite não aparece aqui; para ele vale apenas o que a
/// réplica comporta.
/// </remarks>
internal static class LimitesDoContrato
{
    public const int Nome = 200;
    public const int NomeDoCurso = 200;
    public const int CodigoEmecDoCurso = 20;
    public const int NomeDaUnidade = 200;
    public const int DescricaoDaSituacao = 20;
    public const int VinculoDaSituacao = 4;
}
