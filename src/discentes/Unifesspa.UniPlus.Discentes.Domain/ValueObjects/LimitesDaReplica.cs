namespace Unifesspa.UniPlus.Discentes.Domain.ValueObjects;

/// <summary>
/// Quanto texto a réplica comporta em cada campo do vínculo.
/// </summary>
/// <remarks>
/// São os limites das colunas, declarados aqui para poderem ser conferidos antes da
/// gravação. O contrato da origem declara limite menor para alguns campos e nenhum para
/// outros; onde ela não cumprir o que promete, o excesso derrubaria o lote inteiro na
/// gravação, levando junto os vínculos válidos que o acompanhassem.
/// </remarks>
public static class LimitesDaReplica
{
    public const int Matricula = 20;
    public const int Nivel = 5;
    public const int Nome = 250;
    public const int NomeDoCurso = 250;
    public const int CodigoEmecDoCurso = 20;
    public const int NomeDaUnidade = 250;
    public const int DescricaoDaSituacao = 250;
    public const int VinculoDaSituacao = 100;
}
