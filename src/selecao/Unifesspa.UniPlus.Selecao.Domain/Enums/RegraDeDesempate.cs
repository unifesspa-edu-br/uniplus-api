namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public enum RegraDeDesempate
{
    [CodigoDeContrato("DESEMPATE_MAIOR_NOTA_ETAPA", "Prioriza a maior nota na etapa", Descricao = "Ordena pela nota de uma etapa do processo.")]
    MaiorNotaNaEtapa = 1,

    [CodigoDeContrato("DESEMPATE_MAIOR_IDADE", "Prioriza a maior idade", Descricao = "Ordena pela data de nascimento.")]
    MaiorIdade = 2,

    [CodigoDeContrato("DESEMPATE_IDOSO", "Prioriza a pessoa idosa", Descricao = "Prioriza quem atinge a idade mínima definida.")]
    Idoso = 3,

    [CodigoDeContrato("DESEMPATE_CONDICAO_SOBRE_DADO", "Prioriza quem atende a uma condição", Descricao = "Prioriza quem atende a uma condição sobre um dado do candidato.")]
    CondicaoSobreDado = 4,

    [CodigoDeContrato("DESEMPATE_MAIOR_NOTA_AREA_ENEM", "Prioriza a maior nota por área do ENEM", Descricao = "Ordena pela maior nota nas áreas do ENEM, na ordem declarada.")]
    MaiorNotaPorAreaDoEnem = 5,

}
