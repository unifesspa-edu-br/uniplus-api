namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A proposta de versão do perfil existente. A permissão que ela retira vale na hora, como a remoção avulsa, e o resto
/// vira a versão pendente, tudo sob a trava e na transação de <see cref="OperacaoSobreOPerfil"/>: se a pendente é
/// recusada, a remoção também não acontece. A regra vive no agregado; aqui se traduz o pedido e se aplicam os efeitos.
/// </summary>
public sealed class PropostasDeVersao
{
    private readonly OperacaoSobreOPerfil _operacao;
    private readonly LeitorDoContextoDaVersao _leitor;
    private readonly EfeitosDoDesfechoDaVersao _efeitos;
    private readonly EnvioDoDocumento _envio;
    private readonly AutoriaDaOperacao _autoria;
    private readonly ICatalogoDePermissoesConsultavel _catalogo;

    public PropostasDeVersao(
        OperacaoSobreOPerfil operacao,
        LeitorDoContextoDaVersao leitor,
        EfeitosDoDesfechoDaVersao efeitos,
        EnvioDoDocumento envio,
        AutoriaDaOperacao autoria,
        ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(operacao);
        ArgumentNullException.ThrowIfNull(leitor);
        ArgumentNullException.ThrowIfNull(efeitos);
        ArgumentNullException.ThrowIfNull(envio);
        ArgumentNullException.ThrowIfNull(autoria);
        ArgumentNullException.ThrowIfNull(catalogo);

        _operacao = operacao;
        _leitor = leitor;
        _efeitos = efeitos;
        _envio = envio;
        _autoria = autoria;
        _catalogo = catalogo;
    }

    public async Task<GravacaoDaOperacao<ResultadoDaPropostaDto>> ProporAsync(
        Guid perfilId, ProporVersaoRequest dados, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dados);

        if (await _autoria.DaVersaoAsync(perfilId, cancellationToken).ConfigureAwait(false) is not { } autoria)
        {
            return Recusada(Result<ResultadoDaPropostaDto>.Failure(PerfilHandlerErros.AutorNaoIdentificado()));
        }

        Result<ConteudoDaVersao> conteudo = Validar(dados);
        if (conteudo.IsFailure)
        {
            return Recusada(Result<ResultadoDaPropostaDto>.ValidationFailure(conteudo.Errors));
        }

        if (dados.CategoriaDaRemocao is not null && CategoriaDaRemocaoCodigo.TentarLer(dados.CategoriaDaRemocao) is null)
        {
            return Recusada(Result<ResultadoDaPropostaDto>.Failure(PerfilErros.RemocaoInvalida()));
        }

        return await _operacao
            .ExecutarComAvisosAsync(
                perfilId,
                null,
                (perfil, contexto) => ProporNoPerfilAsync(perfil, Proposta(conteudo.Value!, dados, autoria, contexto.Agora), cancellationToken),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static GravacaoDaOperacao<ResultadoDaPropostaDto> Recusada(Result<ResultadoDaPropostaDto> resultado) =>
        GravacaoDaOperacao<ResultadoDaPropostaDto>.Sem(resultado);

    private static PropostaDeVersao Proposta(ConteudoDaVersao conteudo, ProporVersaoRequest dados, AutoriaDaVersao autoria, DateTimeOffset agora) =>
        new(
            conteudo,
            MotivosDaVersao.Normalizar(dados.MotivoDaRemocao),
            CategoriaDaRemocaoCodigo.TentarLer(dados.CategoriaDaRemocao),
            autoria,
            agora);

    private Result<ConteudoDaVersao> Validar(ProporVersaoRequest dados) =>
        ConteudoDaVersao.Validar(
            dados.Nome, dados.Descricao, dados.Permissoes, dados.Protocolo, dados.TamanhoDoDocumento, _catalogo);

    private async Task<DesfechoDaOperacao<ResultadoDaPropostaDto>> ProporNoPerfilAsync(
        Perfil perfil, PropostaDeVersao proposta, CancellationToken cancellationToken)
    {
        ContextoDaVersao contexto = await _leitor.LerAsync(perfil.Id, proposta.Conteudo.Nome, cancellationToken).ConfigureAwait(false);
        DesfechoDaVersao desfecho = perfil.Propor(proposta, contexto, _catalogo);
        DesfechoDaOperacao<PerfilDto> efeitos = await _efeitos
            .AplicarAsync(perfil, desfecho, proposta.Autoria.Pessoa, cancellationToken)
            .ConfigureAwait(false);

        return new DesfechoDaOperacao<ResultadoDaPropostaDto>(
            await ResultadoAsync(desfecho, efeitos.Resultado, cancellationToken).ConfigureAwait(false),
            efeitos.GravarMesmoNaFalha,
            efeitos.Avisos);
    }

    private async Task<Result<ResultadoDaPropostaDto>> ResultadoAsync(
        DesfechoDaVersao desfecho, Result<PerfilDto> perfil, CancellationToken cancellationToken)
    {
        if (perfil.IsFailure)
        {
            return Result<ResultadoDaPropostaDto>.Failure(perfil.Error!);
        }

        if (desfecho.Criada?.Documento is not { } documento)
        {
            return Result<ResultadoDaPropostaDto>.Success(new ResultadoDaPropostaDto(null, perfil.Value));
        }

        EnvioDoDocumentoDto envio = await _envio.EmitirAsync(documento, cancellationToken).ConfigureAwait(false);

        return Result<ResultadoDaPropostaDto>.Success(new ResultadoDaPropostaDto(envio, null));
    }
}
