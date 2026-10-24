namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Security.Cryptography;

using Abstractions;

using Domain.Interfaces;

using Kernel.Results;

/// <summary>
/// O passo 3 do envio direto, comum ao modelo de documento e ao documento a publicar: lê o arquivo
/// do armazenamento, valida o conteúdo contra o formato declarado, calcula o SHA-256 no servidor,
/// reivindica a confirmação e grava a cópia selada. Quem confirma só decide as recusas que carregam
/// o nome do próprio arquivo e persiste o registro depois. Só para o arquivo do administrador.
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
        IArquivoDoAdministradorRepository repositorio,
        IArquivoArmazenadoStorage storage,
        TimeProvider clock,
        CancellationToken cancellationToken)
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

        string hashSha256 = Convert.ToHexStringLower(SHA256.HashData(lido.Conteudo));

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
        await storage
            .SalvarConteudoSeladoAsync(arquivo.ObjectKeyConfirmado!, lido.Conteudo, arquivo.TipoDeMidia, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success();
    }
}
