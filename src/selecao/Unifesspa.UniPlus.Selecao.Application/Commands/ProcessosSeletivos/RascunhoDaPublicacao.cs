namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Kernel.Results;

/// <summary>
/// As recusas do rascunho da publicação que nascem na Application, e não no domínio: elas
/// dependem do contexto da requisição (quem está autenticado, que processo é este), que a
/// entidade não enxerga.
/// </summary>
/// <remarks>
/// Os códigos são literais, e não montados por interpolação — é assim que a varredura de
/// cobertura do registro de erros consegue encontrá-los, e um código que ela não vê vira
/// resposta não mapeada em vez do 4xx nomeado.
/// </remarks>
internal static class RascunhoDaPublicacao
{
    internal static readonly DomainError SemDono = new(
        "RascunhoDePublicacao.SemUsuarioAutenticado",
        "O rascunho da publicação pertence a quem o escreve, e a requisição não identifica nenhum usuário autenticado.");

    /// <summary>
    /// Duas gravações do mesmo operador no mesmo processo chegando juntas: as duas leem que
    /// não há rascunho e as duas inserem, e o índice único deixa passar uma só. É conflito
    /// transitório — a segunda tentativa encontra a linha da primeira e a substitui —, não
    /// corpo inválido, e por isso não pode chegar ao operador como falha de servidor.
    /// </summary>
    internal static readonly DomainError GravacaoConcorrente = new(
        "RascunhoDaPublicacao.GravacaoConcorrente",
        "Outra gravação deste rascunho chegou primeiro — tente novamente.");

    internal static DomainError ProcessoNaoEncontrado(Guid processoSeletivoId) => new(
        "ProcessoSeletivo.NaoEncontrado",
        $"Processo Seletivo {processoSeletivoId} não encontrado.");
}
