namespace Unifesspa.UniPlus.Host.IntegrationTests.Transacao;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;

/// <summary>Como a gravação nos dois módulos termina: o que o handler de teste faz depois de ter gravado nos dois.</summary>
public enum DesfechoDaGravacao
{
    Sucesso,
    ExcecaoDepoisDasGravacoes,
    RecusaDescartandoAsAlteracoes,
    RecusaSemDescartar,
}

/// <summary>Comando de teste que grava uma entidade da Seleção e uma de Publicações na mesma operação.</summary>
public sealed record GravarEmDoisModulos(string Codigo, DesfechoDaGravacao Desfecho) : ICommand<Result>;

/// <summary>Evento que o handler de teste emite ao outbox quando a operação dá certo.</summary>
public sealed record GravacaoEmDoisModulosConcluida(string Codigo);

/// <summary>
/// Handler de teste de uma operação que grava em dois módulos: recebe a unidade de trabalho do escopo, que o codegen do
/// Wolverine resolve por service location (ADR-0098), e os repositórios públicos de cada módulo, que trazem o contexto
/// único à transação do outbox (ADR-0004, ADR-0170).
/// </summary>
public static class GravarEmDoisModulosHandler
{
    private static readonly DomainError RecusaDeNegocio = new("teste.gravacao_recusada", "A operação foi recusada depois das gravações.");

    public static async Task<(Result, IEnumerable<object>)> Handle(
        GravarEmDoisModulos comando,
        IMotivoDeDecisaoRepository motivos,
        ITipoAtoPublicadoRepository tipos,
        IUnitOfWork unidade,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);
        ArgumentNullException.ThrowIfNull(motivos);
        ArgumentNullException.ThrowIfNull(tipos);
        ArgumentNullException.ThrowIfNull(unidade);

        await motivos.AdicionarAsync(NovoMotivo(comando.Codigo), cancellationToken);
        await tipos.AdicionarAsync(NovoTipo(comando.Codigo), cancellationToken);

        return await ConcluirAsync(comando, unidade, cancellationToken);
    }

    internal static TipoAtoPublicado NovoTipo(string codigo) =>
        TipoAtoPublicado.Criar(
            codigo, "Tipo de ato de teste da transação", congelaConfiguracao: false, unicoPorObjeto: false,
            efeitoIrreversivel: false, ehResultado: false, new DateOnly(2026, 1, 1), vigenciaFim: null, baseLegal: null).Value!;

    internal static MotivoDeDecisao NovoMotivo(string codigo) =>
        MotivoDeDecisao.Criar(new CamposDoMotivo(
            TipoMotivoDeDecisao.Classificacao, codigo, "Motivo de teste da transação.", Subclassificacao: null, SentidoDoMotivo.Favoravel)).Value!;

    private static async Task<(Result, IEnumerable<object>)> ConcluirAsync(
        GravarEmDoisModulos comando, IUnitOfWork unidade, CancellationToken cancellationToken)
    {
        switch (comando.Desfecho)
        {
            case DesfechoDaGravacao.Sucesso:
                await unidade.SalvarAlteracoesAsync(cancellationToken);
                return (Result.Success(), [new GravacaoEmDoisModulosConcluida(comando.Codigo)]);
            case DesfechoDaGravacao.ExcecaoDepoisDasGravacoes:
                await unidade.SalvarAlteracoesAsync(cancellationToken);
                throw new InvalidOperationException("Falha depois de gravar nos dois módulos.");
            case DesfechoDaGravacao.RecusaDescartandoAsAlteracoes:
                unidade.DescartarAlteracoesNaoSalvas();
                return (Result.Failure(RecusaDeNegocio), []);
            default:
                return (Result.Failure(RecusaDeNegocio), []);
        }
    }
}
