namespace Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Persistence;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Publica o documento avulso ou o item do edital já conferido: grava o registro do ato do processo e a entrada do ato na divulgação do certame, entrega
/// o ato à porta de escrita do registro central e salva tudo numa gravação só, que enfileira a cópia dos documentos ao
/// acervo na mesma transação (ADR-0170).
/// </summary>
/// <remarks>
/// <para>
/// <b>Idempotência.</b> Vem da <c>Idempotency-Key</c> do endpoint: o <c>AtoId</c> é decidido a cada
/// requisição e fica no registro do ato.
/// </para>
/// <para>
/// <b>Campos que o documento não declara.</b> O órgão é a sigla da unidade administradora do processo, a
/// série de numeração é o próprio tipo do ato e o ano é o da data que o documento declara. A unidade e
/// o identificador do processo na chave do documento no acervo são os da versão divulgada, gravados no
/// registro porque a chave não muda depois de publicada.
/// </para>
/// </remarks>
public sealed class PublicacaoDoAtoDoProcesso(
    IAtoDoProcessoRepository atoDoProcessoRepository,
    IRegistroDeAtoNormativo registroCentralDeAtos,
    IUnitOfWork unitOfWork)
{
    internal async Task<(Result<AtoPublicadoDto> Resposta, IEnumerable<object> Eventos)> PublicarAsync(
        AtoAPublicar ato,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ato);

        Result<AtoDoProcesso> registrado = await RegistrarAsync(ato, cancellationToken).ConfigureAwait(false);

        return registrado.IsFailure
            ? (Result<AtoPublicadoDto>.ValidationFailure(registrado.Errors), [])
            : await ConcluirPublicacaoAsync(ato, registrado.Value!, cancellationToken).ConfigureAwait(false);
    }

    private async Task<(Result<AtoPublicadoDto> Resposta, IEnumerable<object> Eventos)> ConcluirPublicacaoAsync(
        AtoAPublicar ato,
        AtoDoProcesso atoDoProcesso,
        CancellationToken cancellationToken)
    {
        Result gravado = await RegistrarEGravarAsync(ato, atoDoProcesso, cancellationToken).ConfigureAwait(false);

        return gravado.IsFailure
            ? (Result<AtoPublicadoDto>.ValidationFailure(gravado.Errors), [])
            : (Result<AtoPublicadoDto>.Success(new AtoPublicadoDto(atoDoProcesso.Id)), [new CopiarDocumentosDoAtoAoAcervo(ato.Dados.ProcessoSeletivoId, atoDoProcesso.Id)]);
    }

    private async Task<Result> RegistrarEGravarAsync(
        AtoAPublicar ato,
        AtoDoProcesso atoDoProcesso,
        CancellationToken cancellationToken)
    {
        Result aceito = await registroCentralDeAtos
            .RegistrarAsync(AtoPublicadoPeloProcesso.DoAtoSemVersao(ato, atoDoProcesso), cancellationToken)
            .ConfigureAwait(false);
        if (aceito.IsFailure)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return aceito;
        }

        ato.Processo.Divulgado.AbrirEntradaDoAto(atoDoProcesso.Id, numeroVersao: null);
        return await SalvarAsync(atoDoProcesso, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> SalvarAsync(
        AtoDoProcesso atoDoProcesso, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            return Result.Success();
        }
        catch (Exception excecao) when (Traduzir(excecao, atoDoProcesso) is { } recusa)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(recusa);
        }
    }

    // A Seleção reconhece o que é dela, e a porta, o que é do registro central: quem não reconhece propaga.
    private DomainError? Traduzir(Exception excecao, AtoDoProcesso atoDoProcesso) =>
        ConflitoNaGravacaoDoAtoDoProcesso.Traduzir(ConstraintVioladaNaGravacao.NomeDaConstraintViolada(excecao), atoDoProcesso)
            ?? registroCentralDeAtos.TraduzirConflitoDaGravacao(excecao);

    private async Task<Result<AtoDoProcesso>> RegistrarAsync(
        AtoAPublicar ato, CancellationToken cancellationToken)
    {
        AtoDoProcesso versaoDivulgada = await VersaoDivulgadaAsync(ato, cancellationToken).ConfigureAwait(false);
        Result<AtoDoProcesso> registrado = AtoDoProcesso.Registrar(NovoAtoDe(ato, versaoDivulgada), ato.PublicadoEm);
        if (registrado.IsSuccess)
        {
            await atoDoProcessoRepository.AdicionarAsync(registrado.Value!, cancellationToken).ConfigureAwait(false);
        }

        return registrado;
    }

    private async Task<AtoDoProcesso> VersaoDivulgadaAsync(
        AtoAPublicar ato, CancellationToken cancellationToken)
    {
        Guid atoCriadorId = ato.Processo.Divulgado.AtoCriadorId;

        return await atoDoProcessoRepository.ObterComAnexosAsync(atoCriadorId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"A divulgação do processo {ato.Dados.ProcessoSeletivoId} aponta para o ato {atoCriadorId}, que não tem registro do ato do processo.");
    }

    private static NovoAtoDoProcesso NovoAtoDe(AtoAPublicar ato, AtoDoProcesso versaoDivulgada) =>
        IdentidadeDoNovoAto(ato, versaoDivulgada) with
        {
            ItemDoEdital = ato.ItemDoEdital,
            AtoCorrigidoId = ato.Dados.AtoCorrigidoId,
            MotivoDaCorrecao = ato.Dados.MotivoDaCorrecao,
            PoliticaDeDivulgacaoConfirmadaPorContaId = ato.PoliticaConfirmadaPorContaId,
        };

    private static NovoAtoDoProcesso IdentidadeDoNovoAto(AtoAPublicar ato, AtoDoProcesso versaoDivulgada) =>
        new(
            AtoId: Guid.CreateVersion7(ato.PublicadoEm),
            ProcessoSeletivoId: ato.Dados.ProcessoSeletivoId,
            TipoCodigo: ato.Tipo.Codigo,
            TipoNome: ato.Tipo.Nome,
            Numero: ato.Dados.Numero,
            DataDoAto: ato.DataDoAto,
            Assinante: ato.Dados.Assinante,
            IdentificadorNoEnderecoWebDaUnidadeAdministradora: versaoDivulgada.IdentificadorNoEnderecoWebDaUnidadeAdministradora,
            IdentificadorLegivelDoProcesso: versaoDivulgada.IdentificadorLegivelDoProcesso,
            DocumentoPrincipalId: ato.Documentos.Principal.Id,
            Anexos: [.. ato.Documentos.Anexos.Select(static a => new AnexoDeclarado(a.Documento.Id, a.Nome))]);

}
