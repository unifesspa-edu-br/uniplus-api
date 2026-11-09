namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A criação do perfil com a versão 1. A proposta de versão do perfil existente é de <see cref="PropostasDeVersao"/>.
/// </summary>
public sealed class VersoesDoPerfil
{
    private readonly OperacaoSobreOPerfil _operacao;
    private readonly LeitorDoContextoDaVersao _leitor;
    private readonly EnvioDoDocumento _envio;
    private readonly AutoriaDaOperacao _autoria;
    private readonly ICatalogoDePermissoesConsultavel _catalogo;

    public VersoesDoPerfil(
        OperacaoSobreOPerfil operacao,
        LeitorDoContextoDaVersao leitor,
        EnvioDoDocumento envio,
        AutoriaDaOperacao autoria,
        ICatalogoDePermissoesConsultavel catalogo)
    {
        ArgumentNullException.ThrowIfNull(operacao);
        ArgumentNullException.ThrowIfNull(leitor);
        ArgumentNullException.ThrowIfNull(envio);
        ArgumentNullException.ThrowIfNull(autoria);
        ArgumentNullException.ThrowIfNull(catalogo);

        _operacao = operacao;
        _leitor = leitor;
        _envio = envio;
        _autoria = autoria;
        _catalogo = catalogo;
    }

    /// <summary>Valida o payload por inteiro antes de qualquer I/O e só então confere a unicidade do nome já normalizado.</summary>
    public async Task<Result<EnvioDoDocumentoDto>> CriarPerfilAsync(DadosDaVersaoRequest dados, CancellationToken cancellationToken)
    {
        Result<Perfil> criado = await NovoPerfilAsync(dados, cancellationToken).ConfigureAwait(false);
        if (criado.IsFailure)
        {
            return Result<EnvioDoDocumentoDto>.ValidationFailure(criado.Errors);
        }

        EnvioDoDocumentoDto envio = await _envio
            .EmitirAsync(criado.Value!.VersaoPendente!.Documento!, cancellationToken)
            .ConfigureAwait(false);

        return await _operacao
            .AdicionarAsync(criado.Value, Result<EnvioDoDocumentoDto>.Success(envio), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result<Perfil>> NovoPerfilAsync(DadosDaVersaoRequest dados, CancellationToken cancellationToken)
    {
        if (await _autoria.DaVersaoAsync(null, cancellationToken).ConfigureAwait(false) is not { } autoria)
        {
            return Result<Perfil>.Failure(PerfilHandlerErros.AutorNaoIdentificado());
        }

        Result<ConteudoDaVersao> conteudo = Validar(dados);
        if (conteudo.IsFailure)
        {
            return Result<Perfil>.ValidationFailure(conteudo.Errors);
        }

        return await _leitor.NomeEmUsoPorOutroAsync(conteudo.Value!.Nome, null, cancellationToken).ConfigureAwait(false)
            ? Result<Perfil>.Failure(PerfilErros.NomeDuplicado())
            : Perfil.Criar(conteudo.Value, autoria, _autoria.Agora, _catalogo);
    }

    private Result<ConteudoDaVersao> Validar(DadosDaVersaoRequest dados)
    {
        ArgumentNullException.ThrowIfNull(dados);

        return ConteudoDaVersao.Validar(
            dados.Nome, dados.Descricao, dados.Permissoes, dados.Protocolo, dados.TamanhoDoDocumento, _catalogo);
    }
}
