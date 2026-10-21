namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Security.Cryptography;

using Abstractions;

using Domain.Interfaces;

using Kernel.Results;

/// <summary>
/// O passo 3 do envio direto, comum ao modelo de documento e ao documento a publicar: lê o arquivo
/// do armazenamento, valida o conteúdo contra o formato declarado, calcula o SHA-256 no servidor,
/// reivindica a confirmação e grava a cópia selada. Quem confirma só decide as recusas que carregam
/// o nome do próprio arquivo e persiste o registro depois. O arquivo do candidato passa a
/// <see cref="IProtecaoDoConteudoSelado"/>: verificado pelo antivírus antes de qualquer gravação e
/// selado cifrado.
/// </summary>
public static class ConfirmacaoDoArquivoEnviado
{
    /// <summary>As recusas que o pipeline não conhece, porque o código de erro é de cada arquivo.</summary>
    public sealed record Recusas(DomainError ObjetoNaoEncontrado, DomainError TamanhoExcedido, DomainError StatusInvalido);

    /// <summary>
    /// Confirma o arquivo em memória; quem chama grava o registro (<c>Atualizar</c> e
    /// <c>SalvarAlteracoes</c>) em seguida.
    /// </summary>
    public static async Task<Result> ConfirmarAsync(
        IArquivoEnviado arquivo,
        long tamanhoMaximoBytes,
        Recusas recusas,
        IArquivoEnviadoRepository repositorio,
        IArquivoArmazenadoStorage storage,
        TimeProvider clock,
        CancellationToken cancellationToken,
        IProtecaoDoConteudoSelado? protecao = null,
        Func<CancellationToken, Task<Result>>? aoReivindicar = null)
    {
        ArgumentNullException.ThrowIfNull(arquivo);
        ArgumentNullException.ThrowIfNull(recusas);
        ArgumentNullException.ThrowIfNull(repositorio);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(clock);

        ArquivoEnviadoLido lido = await LeituraDoArquivoEnviado
            .LerAsync(storage, arquivo.ObjectKey, tamanhoMaximoBytes, cancellationToken)
            .ConfigureAwait(false);
        if (lido.Situacao == SituacaoDoArquivoEnviado.Ausente)
        {
            return Result.Failure(recusas.ObjetoNaoEncontrado);
        }

        if (lido.Situacao == SituacaoDoArquivoEnviado.Excedido)
        {
            return Result.Failure(recusas.TamanhoExcedido);
        }

        Result validacao = arquivo.ValidarConteudo(lido.Conteudo.LongLength, lido.ContentType, lido.Conteudo);
        if (validacao.IsFailure)
        {
            return validacao;
        }

        // O antivírus vem antes da reivindicação: o arquivo recusado ou não verificado continua
        // pendente, e nada foi gravado.
        if (protecao is not null)
        {
            Result verificacao = await protecao.VerificarAsync(lido.Conteudo, cancellationToken).ConfigureAwait(false);
            if (verificacao.IsFailure)
            {
                return verificacao;
            }
        }

        string hashSha256 = Convert.ToHexStringLower(SHA256.HashData(lido.Conteudo));

        // O recurso a que o arquivo pertence é disputado só depois de o conteúdo valer: a recusa por
        // formato ou por antivírus não deixa rastro nele.
        if (aoReivindicar is not null)
        {
            Result disputa = await aoReivindicar(cancellationToken).ConfigureAwait(false);
            if (disputa.IsFailure)
            {
                return disputa;
            }
        }

        // A reivindicação vem depois de validar — reivindicar antes travaria o arquivo como
        // confirmado se a validação recusasse — e garante que, entre confirmações concorrentes,
        // só a vencedora escreve a cópia selada.
        if (!await repositorio.TentarReivindicarConfirmacaoAsync(arquivo.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(recusas.StatusInvalido);
        }

        // A reivindicação não sincroniza o rastreamento, então o arquivo segue pendente em memória
        // e a guarda de Confirmar não dispara. Se disparar, o UPDATE já saiu e não há resposta de
        // negócio segura: lançar reverte a transação em vez de confirmar sem hash, tamanho e cópia
        // selada.
        Result confirmacao = arquivo.Confirmar(lido.Conteudo.LongLength, hashSha256, clock);
        if (confirmacao.IsFailure)
        {
            throw new InvalidOperationException(
                $"A confirmação do arquivo {arquivo.Id} falhou após a reivindicação atômica ({confirmacao.Error!.Code}); revertendo a transação.");
        }

        // A cópia selada é o que torna o arquivo confirmado imutável de fato: a chave de envio
        // segue sobrescrevível até o TTL da URL expirar, mas a selada nunca foi exposta por URL
        // pré-assinada.
        // Com proteção, o que se guarda é o conteúdo cifrado, que não abre sem decifrar.
        byte[] aGravar = protecao is null
            ? lido.Conteudo
            : await protecao.SelarAsync(lido.Conteudo, cancellationToken).ConfigureAwait(false);
        string tipoDeMidia = protecao is null ? arquivo.TipoDeMidia : "application/octet-stream";
        await storage
            .SalvarConteudoSeladoAsync(arquivo.ObjectKeyConfirmado!, aGravar, tipoDeMidia, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }
}
