namespace Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Repositories;

/// <summary>
/// O bloqueio de linha que o banco aplica, escolhido entre os que o módulo conhece. O texto é fixo e o identificador da
/// linha entra sempre como parâmetro, na posição <c>{1}</c>: nada que chega de fora compõe o comando.
/// </summary>
internal sealed class InstrucaoDeBloqueio
{
    private InstrucaoDeBloqueio(string comando) => Comando = comando;

    /// <summary>A linha do processo, exclusiva: quem muta o processo, inclusive publicação e retificação.</summary>
    public static InstrucaoDeBloqueio ProcessoExclusivo { get; } =
        new("SELECT 1 FROM selecao.processos_seletivos WHERE id = {1} FOR UPDATE");

    /// <summary>A linha do rascunho, exclusiva: quem substitui, exclui ou publica o rascunho.</summary>
    public static InstrucaoDeBloqueio RascunhoExclusivo { get; } =
        new("SELECT 1 FROM selecao.rascunhos_processos_seletivos WHERE id = {1} FOR UPDATE");

    /// <summary>A linha do rascunho, compartilhada: convive com outra compartilhada e espera a exclusiva.</summary>
    public static InstrucaoDeBloqueio RascunhoCompartilhado { get; } =
        new("SELECT 1 FROM selecao.rascunhos_processos_seletivos WHERE id = {1} FOR SHARE");

    public string Comando { get; }
}
