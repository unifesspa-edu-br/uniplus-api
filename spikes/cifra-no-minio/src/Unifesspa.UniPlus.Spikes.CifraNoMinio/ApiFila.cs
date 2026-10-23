namespace Unifesspa.UniPlus.Spikes.CifraNoMinio;

using Microsoft.EntityFrameworkCore;

using Wolverine.EntityFrameworkCore;

/// <summary>
/// A confirmação do envio no fluxo com fila: a API grava o arquivo como "em verificação" e publica o
/// pedido de verificação pelo outbox do Wolverine, na mesma transação, e depois consome o veredicto.
/// Nada aqui lê o objeto: a API só conhece a chave.
/// </summary>
public static class ApiFila
{
    private const string EmVerificacao = "EM_VERIFICACAO";

    public static async Task PrepararAsync(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        await using AsyncServiceScope escopo = app.Services.CreateAsyncScope();
        Banco banco = escopo.ServiceProvider.GetRequiredService<Banco>();
        await banco.Database.ExecuteSqlRawAsync("""
            CREATE SCHEMA IF NOT EXISTS poc;
            CREATE TABLE IF NOT EXISTS poc.arquivo_enviado (
                "Chave" text PRIMARY KEY,
                "Estado" text NOT NULL,
                "ConfirmadoEm" timestamptz NOT NULL,
                "InicioDaVerificacaoEm" timestamptz,
                "FimDaVerificacaoEm" timestamptz,
                "RegistradoEm" timestamptz,
                "VerificacaoMs" double precision,
                "Veredicto" text,
                "Sha256" text,
                "Bytes" bigint,
                "Verificador" text,
                "Veredictos" integer NOT NULL DEFAULT 0,
                "Publicacoes" integer NOT NULL DEFAULT 0);
            CREATE INDEX IF NOT EXISTS ix_arquivo_enviado_estado ON poc.arquivo_enviado ("Estado", "ConfirmadoEm");
            """);
    }

    public static void Mapear(WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Confirmar de novo o mesmo arquivo não publica outro pedido.
        app.MapPost("/confirmar/{chave}", async (string chave, IDbContextOutbox<Banco> outbox, CancellationToken cancellationToken) =>
        {
            if (await outbox.DbContext.Arquivos.AsNoTracking().FirstOrDefaultAsync(a => a.Chave == chave, cancellationToken) is { } existente)
            {
                return Results.Ok(new { existente.Estado });
            }

            DateTimeOffset agora = DateTimeOffset.UtcNow;
            outbox.DbContext.Arquivos.Add(new ArquivoEnviado { Chave = chave, Estado = EmVerificacao, ConfirmadoEm = agora, Publicacoes = 1 });
            await outbox.PublishAsync(new VerificarArquivo(chave, agora));
            await outbox.SaveChangesAndFlushMessagesAsync(cancellationToken);
            return Results.Accepted(value: new { Estado = EmVerificacao });
        });

        app.MapGet("/arquivos/{chave}/estado", async (string chave, Banco banco, CancellationToken cancellationToken) =>
            await banco.Arquivos.AsNoTracking().FirstOrDefaultAsync(a => a.Chave == chave, cancellationToken) is { } arquivo
                ? Results.Ok(arquivo)
                : Results.NotFound());

        app.MapGet("/arquivos/resumo", async (Banco banco, CancellationToken cancellationToken) =>
            Results.Ok(await banco.Arquivos.AsNoTracking()
                .GroupBy(a => a.Estado)
                .Select(g => new { Estado = g.Key, Arquivos = g.Count(), Veredictos = g.Sum(a => a.Veredictos), Publicacoes = g.Sum(a => a.Publicacoes) })
                .ToListAsync(cancellationToken)));

        app.MapDelete("/arquivos", async (Banco banco, CancellationToken cancellationToken) =>
            Results.Ok(await banco.Arquivos.ExecuteDeleteAsync(cancellationToken)));

        // A rede de segurança: o que está em verificação há mais de N segundos ganha um novo pedido.
        // Em produção roda periodicamente; aqui é chamada pelos roteiros das provas.
        app.MapPost("/reconciliar", async (int segundos, IDbContextOutbox<Banco> outbox, CancellationToken cancellationToken) =>
        {
            DateTimeOffset limite = DateTimeOffset.UtcNow.AddSeconds(-segundos);
            List<ArquivoEnviado> pendentes = await outbox.DbContext.Arquivos
                .Where(a => a.Estado == EmVerificacao && a.ConfirmadoEm < limite)
                .ToListAsync(cancellationToken);
            foreach (ArquivoEnviado arquivo in pendentes)
            {
                arquivo.Publicacoes++;
                await outbox.PublishAsync(new VerificarArquivo(arquivo.Chave, arquivo.ConfirmadoEm));
            }

            await outbox.SaveChangesAndFlushMessagesAsync(cancellationToken);
            return Results.Ok(new { republicados = pendentes.Count });
        });
    }
}

/// <summary>
/// O veredicto chega pela fila de resultados. Só o primeiro muda o estado; os repetidos (entrega
/// duplicada, reconciliação de um arquivo que já tinha sido verificado) só são contados.
/// </summary>
public sealed class ResultadoDaVerificacaoHandler
{
    public static async Task Handle(ArquivoVerificado resultado, Banco banco, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(resultado);
        ArgumentNullException.ThrowIfNull(banco);

        ArquivoEnviado? arquivo = await banco.Arquivos.FirstOrDefaultAsync(a => a.Chave == resultado.Chave, cancellationToken);
        if (arquivo is null)
        {
            return;
        }

        arquivo.Veredictos++;
        if (arquivo.Estado != "EM_VERIFICACAO")
        {
            return;
        }

        arquivo.Estado = resultado.Veredicto == "LIMPO" ? "VERIFICADO" : "RECUSADO";
        arquivo.Veredicto = resultado.Veredicto;
        arquivo.Sha256 = resultado.Sha256;
        arquivo.Bytes = resultado.Bytes;
        arquivo.InicioDaVerificacaoEm = resultado.InicioEm;
        arquivo.FimDaVerificacaoEm = resultado.ConcluidoEm;
        arquivo.VerificacaoMs = resultado.VerificacaoMs;
        arquivo.Verificador = resultado.Verificador;
        arquivo.RegistradoEm = DateTimeOffset.UtcNow;
    }
}
