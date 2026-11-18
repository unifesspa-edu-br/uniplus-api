namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// As transições que congelam uma versão a partir de um <see cref="CenarioDeCongelamento"/>: cada
/// chamada monta o pedido e delega ao agregado, sem validar nada além do que o agregado valida.
/// </summary>
internal static class CongelamentoDeTeste
{
    public static Result<VersaoConfiguracao> Publicar(this ProcessoSeletivo processo, CenarioDeCongelamento cenario) =>
        Congelar(
            processo,
            TransicoesDaVersao.ConferirEstadoParaPublicar(processo.Status),
            cenario,
            configuracao => processo.Publicar(Pedido(cenario, configuracao)));

    public static Result<VersaoConfiguracao> Retificar(
        this ProcessoSeletivo processo, CenarioDeCongelamento cenario, VersaoConfiguracao versaoAtual, string motivo) =>
        Congelar(
            processo,
            TransicoesDaVersao.ConferirEstadoParaRetificar(processo.Id, processo.Status, versaoAtual, motivo),
            cenario,
            configuracao => processo.Retificar(Pedido(cenario, configuracao), versaoAtual, motivo));

    // A ordem do congelamento: a conferência do estado da transição, depois a da configuração, e só então a transição, que
    // recebe a configuração apta.
    private static Result<VersaoConfiguracao> Congelar(
        ProcessoSeletivo processo,
        Result doEstado,
        CenarioDeCongelamento cenario,
        Func<ConfiguracaoAptaACongelar, Result<VersaoConfiguracao>> transicao)
    {
        if (doEstado.IsFailure)
        {
            return Result<VersaoConfiguracao>.ValidationFailure(doEstado.Errors);
        }

        Result<ConfiguracaoAptaACongelar> daConfiguracao = ConfiguracaoAptaACongelar.Conferir(processo.Id, processo.Declaracao(), CadastrosDe(processo, cenario));

        return daConfiguracao.IsFailure
            ? Result<VersaoConfiguracao>.ValidationFailure(daConfiguracao.Errors)
            : transicao(daConfiguracao.Value!);
    }

    private static CadastrosDaConfiguracao CadastrosDe(ProcessoSeletivo processo, CenarioDeCongelamento cenario) =>
        CadastrosDeTeste.Com(cenario.Contagem, cenario.FatosDeModalidade, cenario.TiposDeAtoUnicos, cenario.Corretores ?? CorretoresDeTeste.DoProcesso(processo), motivosEscolhidos: cenario.MotivosEscolhidos ?? MotivosDoProcessoDeTeste.Cobrindo(processo)).ComRegrasDo(processo, cenario.RegraAusenteDoCatalogo);

    private static PedidoDeCongelamento Pedido(CenarioDeCongelamento cenario, ConfiguracaoAptaACongelar configuracao) =>
        new()
        {
            Dados = cenario.Dados,
            Documento = cenario.Documento,
            AtorUsuarioContaId = cenario.AtorUsuarioContaId,
            Instante = cenario.Relogio.GetUtcNow(),
            ConfiguracaoApta = configuracao,
        };
}
