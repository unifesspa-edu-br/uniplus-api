namespace Unifesspa.UniPlus.Publicacoes.Infrastructure.Messaging;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Abstractions;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Publicacoes.Infrastructure.Persistence;

using Wolverine.Attributes;
using Wolverine.ErrorHandling;
using Wolverine.Runtime.Handlers;

/// <summary>
/// Handler da <see cref="RegistrarAtoNormativoRequisicao"/>: a mensagem durável que um
/// domínio enfileirou, na mesma transação em que publicou, para que o ato correspondente
/// seja registrado aqui (ADR-0108).
/// </summary>
/// <remarks>
/// <para>
/// <b>Delega ao handler do caminho HTTP</b>, em vez de repetir o registro. Registrar um
/// ato não é inserir uma linha: é verificar que a classe de congelamento do retificador
/// coincide com a do retificado (ADR-0103), que a cadeia não ramifica, que a retificação
/// <b>herda</b> os vínculos do ato que emenda, e que a vaga do objeto é reservada pela
/// RAIZ da linhagem — não pelo ato novo (ADR-0107). Duas cópias dessas invariantes
/// divergem, e a de baixo silencia o que a de cima recusa.
/// </para>
/// <para>
/// <b>Vive em Infrastructure</b> porque o middleware transacional do Wolverine só instala
/// a transação quando enxerga o <c>DbContext</c> CONCRETO na assinatura; com a interface
/// de unidade de trabalho, não instala — e o <c>SaveChanges</c> lá dentro morre com
/// <i>"This NpgsqlTransaction has completed"</i>. O <c>DbContext</c> está na assinatura
/// por isso; os repositórios operam sobre a mesma instância do escopo.
/// </para>
/// <para>
/// <b>Falha LANÇA.</b> Um <c>Result.Failure</c> devolvido de um handler de mensagem é lido
/// pelo Wolverine como processamento bem-sucedido: a mensagem sairia da fila e o ato
/// sumiria em silêncio. Só a exceção aciona o retry e, esgotado, a dead letter.
/// </para>
/// </remarks>
public static class RegistrarAtoNormativoRequisicaoHandler
{
    /// <summary>
    /// Política de falha DESTE handler — e de mais nenhum. O Wolverine reconhece a
    /// assinatura e a aplica só à chain desta mensagem.
    /// </summary>
    /// <remarks>
    /// Declarar isto como política global (<c>opts.Policies.OnException</c>) contaminaria o
    /// processo inteiro: todo command HTTP passa por <c>ICommandBus.Send</c> → <c>InvokeAsync</c>,
    /// que aplica as políticas de retry inline — uma falha de validação em qualquer módulo
    /// passaria a esperar a sequência de cooldown antes de devolver o erro ao cliente.
    /// </remarks>
    public static void Configure(HandlerChain chain)
    {
        ArgumentNullException.ThrowIfNull(chain);

        // Retificação que chegou antes do ato que ela emenda: publicar e retificar em
        // sequência põem DUAS requisições na fila, e nada garante a ordem de processamento.
        // O predecessor não está ausente — está a caminho, e insistir resolve. Mandar para a
        // dead letter deixaria Seleção com o edital retificado e Publicações sem o ato.
        //
        // Declarada ANTES da regra geral: a primeira política que casa é a que vale.
        chain.OnException<RegistroDeAtoRecusadoException>(ex => ex.AguardaPredecessor)
            .RetryWithCooldown(TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5))
            .Then.MoveToErrorQueue();

        // As demais recusas são de mérito, e não melhoram com o tempo: o tipo continuará sem
        // versão vigente, a classe de congelamento continuará divergente, a vaga continuará
        // ocupada. Vão direto para a dead letter, onde alguém precisa olhar.
        chain.OnException<RegistroDeAtoRecusadoException>()
            .MoveToErrorQueue();

        // Falha transiente (indisponibilidade momentânea, deadlock) é o caso em que insistir
        // resolve — três tentativas espaçadas antes de desistir.
        chain.OnException<Exception>()
            .RetryWithCooldown(TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5))
            .Then.MoveToErrorQueue();
    }

    [Transactional]
    public static async Task Handle(
        RegistrarAtoNormativoRequisicao requisicao,
        PublicacoesDbContext db,
        ITipoAtoPublicadoRepository tiposRepository,
        IAtoNormativoRepository atosRepository,
        IPublicacoesUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(requisicao);
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(atosRepository);

        // O id vem do domínio que publicou, e é ele que torna a reentrega inofensiva. Um
        // Guid.Empty aqui seria lido pela factory como "gere um id novo" — o ato nasceria com
        // identidade diferente da que a versão da configuração já referencia, e cada reentrega
        // criaria mais um. A mensagem sem id não é registrável.
        if (requisicao.AtoId == Guid.Empty)
        {
            throw new RegistroDeAtoRecusadoException(
                "Requisição de registro de ato sem identificador — o id é decidido por quem publica (ADR-0108).");
        }

        // Reentrega: o ato já existe com este id. Nada a fazer — e nada a corrigir.
        AtoNormativo? jaRegistrado = await atosRepository
            .ObterPorIdParaLeituraAsync(requisicao.AtoId, cancellationToken)
            .ConfigureAwait(false);
        if (jaRegistrado is not null)
        {
            return;
        }

        // O tipo tem de ser do CADASTRO — o vocabulário é de Publicações, e um domínio não
        // inventa tipo de ato (ADR-0103). O que vem por valor são os ATRIBUTOS conferidos, não
        // a existência do tipo: sem esta checagem, uma requisição poderia registrar um ato de
        // um tipo que nunca existiu.
        //
        // A busca ignora vigência e exclusão lógica de propósito: o tipo pode ter sido
        // encerrado ou removido entre a publicação e o consumo, e o ato publicado sob ele
        // continua sendo um ato daquele tipo.
        bool tipoDoCadastro = await db.Set<TipoAtoPublicado>()
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(t => t.Codigo == requisicao.TipoCodigo, cancellationToken)
            .ConfigureAwait(false);
        if (!tipoDoCadastro)
        {
            throw new RegistroDeAtoRecusadoException(
                requisicao.AtoId,
                AtoNormativoErrorCodes.TipoSemVersaoVigente,
                $"O tipo de ato '{requisicao.TipoCodigo}' não existe no catálogo.");
        }

        RegistrarAtoNormativoCommand comando = new(
            Orgao: requisicao.Orgao,
            Serie: requisicao.Serie,
            Ano: requisicao.Ano,
            Numero: requisicao.Numero,
            TipoCodigo: requisicao.TipoCodigo,
            DataPublicacao: requisicao.DataPublicacao,
            DocumentoHash: requisicao.DocumentoHash,
            Assinante: requisicao.Assinante,
            VersaoInvocadaId: requisicao.VersaoInvocadaId,
            VersaoInvocadaHash: requisicao.VersaoInvocadaHash,
            AtoRetificadoId: requisicao.AtoRetificadoId,
            MotivoRetificacao: requisicao.MotivoRetificacao,
            Vinculos: [.. requisicao.Vinculos.Select(v => new VinculoEntidadeInput(v.EntidadeTipo, v.EntidadeId))],
            AtoId: requisicao.AtoId,
            // Os atributos que o domínio conferiu ao publicar. Sem repassá-los, o registro
            // releria o catálogo — que é editável — e a decisão tomada no 204 valeria outra
            // coisa (ADR-0061).
            AtributosDoTipo: requisicao.AtributosDoTipo);

        Result<RegistrarAtoNormativoResult> resultado;
        try
        {
            resultado = await RegistrarAtoNormativoCommandHandler
                .Handle(comando, tiposRepository, atosRepository, unitOfWork, timeProvider, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (EhViolacaoDeUnicidade(ex))
        {
            // Duas entregas do MESMO envelope correndo juntas: ambas passaram pela checagem
            // inicial e a segunda esbarrou numa unicidade. Nem toda violação é reentrega — a
            // vaga de linhagem tomada por OUTRA linhagem é conflito real —, então a pergunta
            // não é "qual constraint quebrou", e sim: o ato DESTE envelope acabou registrado?
            db.ChangeTracker.Clear();
            await GarantirRegistradoOuPropagarAsync(ex, requisicao.AtoId, atosRepository, cancellationToken)
                .ConfigureAwait(false);
            return;
        }

        if (resultado.IsFailure)
        {
            // A recusa também pode ser sombra de uma corrida: quando duas entregas da mesma
            // RETIFICAÇÃO se cruzam, a que perde encontra a raiz já retificada — pela outra
            // entrega dela própria — e recusa com RaizJaRetificada, sem nunca chegar à chave
            // primária. Mandar isso para a dead letter pediria intervenção humana para um ato
            // que está registrado, e corretamente.
            db.ChangeTracker.Clear();
            AtoNormativo? registradoPorOutraEntrega = await atosRepository
                .ObterPorIdParaLeituraAsync(requisicao.AtoId, cancellationToken)
                .ConfigureAwait(false);
            if (registradoPorOutraEntrega is not null)
            {
                return;
            }

            throw new RegistroDeAtoRecusadoException(
                requisicao.AtoId, resultado.Error!.Code, resultado.Error!.Message);
        }
    }

    /// <summary>
    /// Uma violação de unicidade só é reentrega se o ato deste envelope existir depois dela.
    /// Se não existir, o conflito é de outro — e precisa aflorar na dead letter, porque
    /// silenciá-lo deixaria o edital publicado sem ato, sem ninguém saber.
    /// </summary>
    private static async Task GarantirRegistradoOuPropagarAsync(
        DbUpdateException ex,
        Guid atoId,
        IAtoNormativoRepository atosRepository,
        CancellationToken cancellationToken)
    {
        AtoNormativo? ato = await atosRepository
            .ObterPorIdParaLeituraAsync(atoId, cancellationToken)
            .ConfigureAwait(false);

        if (ato is null)
        {
            throw new RegistroDeAtoRecusadoException(
                $"Conflito de unicidade ao registrar o ato {atoId}, que não ficou registrado.", ex);
        }
    }

    private static bool EhViolacaoDeUnicidade(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

/// <summary>
/// Recusa de negócio no registro de um ato vindo de mensagem durável. Existe para que a
/// falha ESCAPE do handler: só a exceção faz o Wolverine retentar e, esgotada a política,
/// mover o envelope para a dead letter, onde a falha é visível e reprocessável.
/// </summary>
public sealed class RegistroDeAtoRecusadoException : Exception
{
    public RegistroDeAtoRecusadoException(Guid atoId, string codigo, string mensagem)
        : base($"Registro do ato {atoId} recusado ({codigo}): {mensagem}")
    {
        AtoId = atoId;
        Codigo = codigo;
    }

    public RegistroDeAtoRecusadoException()
    {
    }

    public RegistroDeAtoRecusadoException(string message)
        : base(message)
    {
    }

    public RegistroDeAtoRecusadoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public Guid AtoId { get; }

    public string Codigo { get; } = string.Empty;

    /// <summary>
    /// A recusa é de ORDEM, não de mérito: o ato que esta requisição emenda ainda não foi
    /// registrado.
    /// </summary>
    /// <remarks>
    /// Publicar e retificar em sequência rápida põem DUAS requisições na fila, e nada
    /// garante que a do ato de abertura seja processada primeiro. Se a retificação chegar
    /// antes, o predecessor "não existe" — mas vai existir daqui a milissegundos. Mandar
    /// isso direto para a dead letter deixaria Seleção com o edital retificado e Publicações
    /// sem o ato correspondente, por um problema que o tempo resolve sozinho.
    /// <para>
    /// As demais recusas são de mérito e não melhoram com o tempo: o tipo continuará sem
    /// versão vigente, a classe de congelamento continuará divergente, a vaga continuará
    /// ocupada. Essas vão direto para a dead letter, onde alguém precisa olhar.
    /// </para>
    /// </remarks>
    public bool AguardaPredecessor =>
        string.Equals(Codigo, AtoNormativoErrorCodes.AtoRetificadoNaoEncontrado, StringComparison.Ordinal);
}
