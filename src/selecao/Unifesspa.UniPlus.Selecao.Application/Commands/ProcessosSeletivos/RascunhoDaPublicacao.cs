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

    internal static DomainError ProcessoNaoEncontrado(Guid processoSeletivoId) => new(
        "ProcessoSeletivo.NaoEncontrado",
        $"Processo Seletivo {processoSeletivoId} não encontrado.");
}
