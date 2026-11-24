namespace Unifesspa.UniPlus.Selecao.Domain.Enums;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

public enum RegraDeApuracaoDaAvaliacao
{
    [CodigoDeContrato("APURACAO_AVALIACAO_UNICA", "Usa a avaliação de um avaliador", Descricao = "Usa a nota ou o conceito atribuído por um avaliador.")]
    AvaliacaoUnica = 1,

    [CodigoDeContrato("APURACAO_CORRECAO_INDEPENDENTE", "Combina correções independentes", Descricao = "Combina as notas atribuídas por avaliadores que corrigem independentemente.")]
    CorrecaoIndependente = 2,

    [CodigoDeContrato("APURACAO_CONCEITO_UNANIMIDADE", "Apura o conceito por unanimidade", Descricao = "Exige que todos os avaliadores atribuam o mesmo conceito.")]
    ConceitoUnanimidade = 3,

    [CodigoDeContrato("APURACAO_CONCEITO_MAIORIA_SIMPLES", "Apura o conceito por maioria simples", Descricao = "Escolhe o conceito com mais atribuições que cada um dos demais.")]
    ConceitoMaioriaSimples = 4,

    [CodigoDeContrato("APURACAO_CONCEITO_MAIORIA_ABSOLUTA", "Apura o conceito por maioria absoluta", Descricao = "Escolhe o conceito atribuído por mais da metade dos avaliadores.")]
    ConceitoMaioriaAbsoluta = 5,
}
